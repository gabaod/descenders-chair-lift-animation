using UnityEngine;
using System.Collections.Generic;
using ModTool.Interface;

public class SkiLiftController : ModBehaviour
{
    [Header("Cable Settings")]
    [Tooltip("Speed of the cable in meters per second")]
    public float cableSpeed = 2f;
    
    [Header("References")]
    public LineRenderer cableRenderer;
    public GameObject chairsParent;
    
    [Header("Debug Info")]
    public bool showDebugInfo = true;
    
    private List<Transform> chairs = new List<Transform>();
    private List<float> chairDistances = new List<float>();
    private float cableLength = 0f;
    private float currentOffset = 0f;
    private bool isInitialized = false;
    
    void Awake()
    {
        Debug.Log("SkiLiftController: Awake called");
    }
    
    void Start()
    {
        Debug.Log("SkiLiftController: Start called");
        Initialize();
    }
    
    void OnEnable()
    {
        Debug.Log("SkiLiftController: OnEnable called");
        if (!isInitialized)
        {
            Initialize();
        }
    }
    
    private void Initialize()
    {
        Debug.Log("SkiLiftController: Initializing...");
        
        // Find cable renderer if not assigned
        if (cableRenderer == null)
        {
            cableRenderer = GetComponentInChildren<LineRenderer>();
            if (cableRenderer == null)
            {
                // Try to find in parent
                if (transform.parent != null)
                {
                    cableRenderer = transform.parent.GetComponentInChildren<LineRenderer>();
                }
            }
            
            if (cableRenderer == null)
            {
                Debug.LogError("SkiLiftController: No LineRenderer found! Please assign the cable LineRenderer manually.");
                enabled = false;
                return;
            }
            else
            {
                Debug.Log("SkiLiftController: Found LineRenderer: " + cableRenderer.name);
            }
        }
        
        // Find chairs parent if not assigned
        if (chairsParent == null)
        {
            chairsParent = gameObject;
            Debug.Log("SkiLiftController: Using this GameObject as chairs parent");
        }
        
        InitializeChairs();
        CalculateCableLength();
        
        if (chairs.Count > 0 && cableLength > 0)
        {
            CalculateInitialChairDistances();
            isInitialized = true;
            Debug.Log("SkiLiftController: Initialization complete! Ready to animate.");
        }
        else
        {
            Debug.LogWarning("SkiLiftController: No chairs found or cable length is 0. Cannot initialize.");
        }
    }
    
    void Update()
    {
        if (!isInitialized || chairs.Count == 0 || cableLength == 0)
        {
            if (showDebugInfo && Time.frameCount % 60 == 0)
            {
                Debug.LogWarning("SkiLiftController: Not initialized. Chairs: " + chairs.Count + ", Cable Length: " + cableLength);
            }
            return;
        }
        
        // Move the cable offset
        currentOffset += cableSpeed * Time.deltaTime;
        
        // Loop the offset when it exceeds cable length
        if (currentOffset >= cableLength)
        {
            currentOffset -= cableLength;
        }
        
        // Update chair positions
        UpdateChairPositions();
        
        // Debug info
        if (showDebugInfo && Time.frameCount % 60 == 0)
        {
            Debug.Log("SkiLiftController: Offset = " + currentOffset.ToString("F2") + " / " + cableLength.ToString("F2"));
        }
    }
    
    private void InitializeChairs()
    {
        chairs.Clear();
        
        // Search in chairsParent and all children
        Transform[] allTransforms = chairsParent.GetComponentsInChildren<Transform>();
        
        foreach (Transform t in allTransforms)
        {
            if (t.name.StartsWith("Chair"))
            {
                chairs.Add(t);
                Debug.Log("SkiLiftController: Found chair: " + t.name);
            }
        }
        
        Debug.Log("SkiLiftController: Total chairs found = " + chairs.Count);
    }
    
    private void CalculateCableLength()
    {
        if (cableRenderer == null || cableRenderer.positionCount < 2)
        {
            Debug.LogError("SkiLiftController: Cable renderer has no positions!");
            return;
        }
        
        cableLength = 0f;
        
        for (int i = 0; i < cableRenderer.positionCount - 1; i++)
        {
            Vector3 p1 = cableRenderer.GetPosition(i);
            Vector3 p2 = cableRenderer.GetPosition(i + 1);
            cableLength += Vector3.Distance(p1, p2);
        }
        
        Debug.Log("SkiLiftController: Cable length = " + cableLength + " meters, Points = " + cableRenderer.positionCount);
    }
    
    private void CalculateInitialChairDistances()
    {
        chairDistances.Clear();
        
        foreach (Transform chair in chairs)
        {
            // Find the closest point on the cable to this chair
            float closestDistance = FindDistanceAlongCable(chair.position);
            chairDistances.Add(closestDistance);
            Debug.Log("SkiLiftController: " + chair.name + " at distance " + closestDistance.ToString("F2"));
        }
    }
    
    private float FindDistanceAlongCable(Vector3 position)
    {
        float minDist = float.MaxValue;
        float distanceAlongCable = 0f;
        float travelled = 0f;
        
        for (int i = 0; i < cableRenderer.positionCount - 1; i++)
        {
            Vector3 p1 = cableRenderer.GetPosition(i);
            Vector3 p2 = cableRenderer.GetPosition(i + 1);
            
            // Find closest point on this segment
            Vector3 closestPoint = ClosestPointOnLineSegment(p1, p2, position);
            float dist = Vector3.Distance(position, closestPoint);
            
            if (dist < minDist)
            {
                minDist = dist;
                float segmentDist = Vector3.Distance(p1, closestPoint);
                distanceAlongCable = travelled + segmentDist;
            }
            
            travelled += Vector3.Distance(p1, p2);
        }
        
        return distanceAlongCable;
    }
    
    private Vector3 ClosestPointOnLineSegment(Vector3 a, Vector3 b, Vector3 point)
    {
        Vector3 ab = b - a;
        float t = Vector3.Dot(point - a, ab) / Vector3.Dot(ab, ab);
        t = Mathf.Clamp01(t);
        return a + ab * t;
    }
    
    private void UpdateChairPositions()
    {
        for (int i = 0; i < chairs.Count; i++)
        {
            if (chairs[i] == null)
                continue;
                
            // Calculate this chair's current distance along cable
            float chairDistance = (chairDistances[i] + currentOffset) % cableLength;
            
            // Get position and direction at this distance
            Vector3 position = GetPointOnCable(chairDistance);
            Vector3 direction = GetDirectionOnCable(chairDistance);
            
            // Update chair position and rotation
            chairs[i].position = position;
            
            if (direction != Vector3.zero)
            {
                chairs[i].rotation = Quaternion.LookRotation(direction);
            }
        }
    }
    
    private Vector3 GetPointOnCable(float distance)
    {
        float travelled = 0f;
        
        for (int i = 0; i < cableRenderer.positionCount - 1; i++)
        {
            Vector3 p1 = cableRenderer.GetPosition(i);
            Vector3 p2 = cableRenderer.GetPosition(i + 1);
            float segmentLength = Vector3.Distance(p1, p2);
            
            if (travelled + segmentLength >= distance)
            {
                float t = (distance - travelled) / segmentLength;
                return Vector3.Lerp(p1, p2, t);
            }
            
            travelled += segmentLength;
        }
        
        return cableRenderer.GetPosition(cableRenderer.positionCount - 1);
    }
    
    private Vector3 GetDirectionOnCable(float distance)
    {
        float travelled = 0f;
        
        for (int i = 0; i < cableRenderer.positionCount - 1; i++)
        {
            Vector3 p1 = cableRenderer.GetPosition(i);
            Vector3 p2 = cableRenderer.GetPosition(i + 1);
            float segmentLength = Vector3.Distance(p1, p2);
            
            if (travelled + segmentLength >= distance)
            {
                return (p2 - p1).normalized;
            }
            
            travelled += segmentLength;
        }
        
        // Return direction of last segment
        if (cableRenderer.positionCount >= 2)
        {
            Vector3 p1 = cableRenderer.GetPosition(cableRenderer.positionCount - 2);
            Vector3 p2 = cableRenderer.GetPosition(cableRenderer.positionCount - 1);
            return (p2 - p1).normalized;
        }
        
        return Vector3.forward;
    }
    
    void OnDrawGizmos()
    {
        // Draw the cable path in the editor
        if (cableRenderer != null && cableRenderer.positionCount > 1)
        {
            Gizmos.color = Color.yellow;
            for (int i = 0; i < cableRenderer.positionCount - 1; i++)
            {
                Vector3 p1 = cableRenderer.GetPosition(i);
                Vector3 p2 = cableRenderer.GetPosition(i + 1);
                Gizmos.DrawLine(p1, p2);
            }
        }
        
        // Draw chair positions
        if (chairs != null && chairs.Count > 0)
        {
            Gizmos.color = Color.green;
            foreach (Transform chair in chairs)
            {
                if (chair != null)
                {
                    Gizmos.DrawWireSphere(chair.position, 0.5f);
                }
            }
        }
    }
}
