using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Tower configuration with separate uphill and downhill wheels
/// </summary>
[System.Serializable]
public class SkiLiftTower
{
    [Tooltip("Uphill wheel (for Green to Red path)")]
    public Transform uphillWheel;

    [Tooltip("Downhill wheel (for return path)")]
    public Transform downhillWheel;
}

/// <summary>
/// Main ski lift system that manages towers, wheels, cable, and animated chairs
/// </summary>
public class SkiLiftSystem : MonoBehaviour
{
    [Header("Lift Configuration")]
    [Tooltip("Starting wheel position (bottom station) - assign the actual wheel object or its parent")]
    public Transform startWheel;

    [Tooltip("Ending wheel position (top station) - assign the actual wheel object or its parent")]
    public Transform endWheel;

    [Tooltip("Return wheel at start station (creates loop) - assign the actual wheel object or its parent")]
    public Transform startReturnWheel;

    [Tooltip("Return wheel at end station (creates loop) - assign the actual wheel object or its parent")]
    public Transform endReturnWheel;

    [Tooltip("If wheels are child objects, specify child index (0 = first child, -1 = use parent)")]
    public int wheelChildIndex = 0;

    [Tooltip("Towers along the cable path - each tower has uphill and downhill wheels")]
    public List<SkiLiftTower> towers = new List<SkiLiftTower>();

    [Header("Chair Settings")]
    [Tooltip("Prefab of the chair to instantiate")]
    public GameObject chairPrefab;

    [Tooltip("Number of chairs on the lift")]
    public int chairCount = 10;

    [Tooltip("Spacing between chairs in meters")]
    public float chairSpacing = 15f;

    [Tooltip("Scale multiplier for chairs")]
    [Range(0.1f, 5.0f)]
    public float chairScale = 1.0f;

    [Tooltip("Vertical offset from cable - positive values hang chair below cable")]
    public float chairVerticalOffset = 2f;

    [Header("Animation Settings")]
    [Tooltip("Speed of the lift in meters per second")]
    public float liftSpeed = 2f;

    [Tooltip("Radius of the wheels")]
    public float wheelRadius = 2f;

    [Header("Cable Rendering")]
    [Tooltip("Width of the cable")]
    public float cableWidth = 0.1f;

    [Tooltip("Material for the cable - MUST use a 3D shader like Diffuse or Standard")]
    public Material cableMaterial;

    [Tooltip("Segments per curve section for smooth cable")]
    public int curveSegments = 20;

    [Tooltip("Sag amount for uphill cable (0 = straight, higher = more sag)")]
    [Range(0f, 0.2f)]
    public float uphillCableSag = 0.05f;

    [Tooltip("Sag amount for downhill return cable (0 = straight, higher = more sag)")]
    [Range(0f, 0.2f)]
    public float downhillCableSag = 0.05f;

    [Header("Debug")]
    [Tooltip("Show debug spheres along cable path")]
    public bool showDebugSpheres = false;

    [Tooltip("Show runtime debug visualization (visible in Game view)")]
    public bool showRuntimeDebug = true;

    // Private variables
    private List<GameObject> debugSpheres = new List<GameObject>();
    private List<GameObject> chairs = new List<GameObject>();
    private List<float> chairDistances = new List<float>();
    private List<Vector3> cablePoints = new List<Vector3>();
    private float totalCableLength = 0f;
    private LineRenderer cableRenderer;
    private float currentOffset = 0f;
    private float previousChairScale = 1.0f;

    // Wheel rotation tracking
    private Vector3 startWheelInitialPos;
    private Vector3 endWheelInitialPos;
    private Vector3 startReturnWheelInitialPos;
    private Vector3 endReturnWheelInitialPos;
    private float wheelRotation = 0f;

    // Visual wheel meshes
    private Transform startWheelVisual;
    private Transform endWheelVisual;
    private Transform startReturnWheelVisual;
    private Transform endReturnWheelVisual;

    void Start()
    {
        ValidateSetup();
        DisablePhysicsOnWheels();
        StoreInitialWheelPositions();
        CreateWheelVisuals();
        GenerateCablePoints();
        CreateCableLineRenderer();
        CreateRuntimeDebugVisualization();
        SpawnChairs();
        previousChairScale = chairScale;
    }

    void Update()
    {
        AnimateChairs();
        AnimateWheels();
        UpdateChairScales();
    }

    void ValidateSetup()
    {
        if (startWheel == null || endWheel == null)
        {
            Debug.LogError("SkiLiftSystem: Start and End wheels must be assigned!");
            enabled = false;
            return;
        }

        if (chairPrefab == null)
        {
            Debug.LogError("SkiLiftSystem: Chair prefab must be assigned!");
            enabled = false;
            return;
        }
    }

    void DisablePhysicsOnWheels()
    {
        DisablePhysicsOnTransform(startWheel);
        DisablePhysicsOnTransform(endWheel);
        DisablePhysicsOnTransform(startReturnWheel);
        DisablePhysicsOnTransform(endReturnWheel);
    }

    void DisablePhysicsOnTransform(Transform t)
    {
        if (t == null) return;

        Rigidbody rb = t.GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.isKinematic = true;
            rb.useGravity = false;
        }

        Collider col = t.GetComponent<Collider>();
        if (col != null)
        {
            col.enabled = false;
        }
    }

    void StoreInitialWheelPositions()
    {
        if (startWheel != null)
            startWheelInitialPos = startWheel.position; // World position
        if (endWheel != null)
            endWheelInitialPos = endWheel.position; // World position
        if (startReturnWheel != null)
            startReturnWheelInitialPos = startReturnWheel.position; // World position
        if (endReturnWheel != null)
            endReturnWheelInitialPos = endReturnWheel.position; // World position

        Debug.Log("Stored initial positions - Start: " + startWheelInitialPos + ", End: " + endWheelInitialPos);
    }

    void CreateWheelVisuals()
    {
        // Get the actual wheel transforms (either parent or child based on wheelChildIndex)
        Transform actualStartWheel = GetActualWheelTransform(startWheel);
        Transform actualEndWheel = GetActualWheelTransform(endWheel);
        Transform actualStartReturnWheel = GetActualWheelTransform(startReturnWheel);
        Transform actualEndReturnWheel = GetActualWheelTransform(endReturnWheel);

        if (actualStartWheel != null)
            startWheelVisual = CreateVisualChild(actualStartWheel, "WheelVisual");
        if (actualEndWheel != null)
            endWheelVisual = CreateVisualChild(actualEndWheel, "WheelVisual");
        if (actualStartReturnWheel != null)
            startReturnWheelVisual = CreateVisualChild(actualStartReturnWheel, "WheelVisual");
        if (actualEndReturnWheel != null)
            endReturnWheelVisual = CreateVisualChild(actualEndReturnWheel, "WheelVisual");
    }

    /// <summary>
    /// Gets the actual wheel transform based on wheelChildIndex setting
    /// </summary>
    Transform GetActualWheelTransform(Transform t)
    {
        if (t == null)
            return null;

        if (wheelChildIndex < 0)
            return t;

        if (t.childCount > wheelChildIndex)
            return t.GetChild(wheelChildIndex);

        return t;
    }

    Transform CreateVisualChild(Transform parent, string name)
    {
        Transform visual = parent.Find(name);
        if (visual == null)
        {
            visual = new GameObject(name).transform;
            visual.SetParent(parent, false);
            visual.localPosition = Vector3.zero;
            visual.localRotation = Quaternion.identity;

            List<Transform> children = new List<Transform>();
            foreach (Transform child in parent)
            {
                if (child != visual)
                    children.Add(child);
            }

            foreach (Transform child in children)
            {
                child.SetParent(visual, true);
            }
        }
        return visual;
    }

    void GenerateCablePoints()
    {
        cablePoints.Clear();

        // IMPORTANT: Get WORLD positions of wheel transforms ONCE and store them
        Vector3 startWheelPos = GetWorldPosition(startWheel);      // GREEN
        Vector3 endWheelPos = GetWorldPosition(endWheel);          // RED
        Vector3 startReturnPos = GetWorldPosition(startReturnWheel);  // CYAN
        Vector3 endReturnPos = GetWorldPosition(endReturnWheel);      // MAGENTA

        // Create default positions if return wheels not assigned
        if (startReturnWheel == null)
            startReturnPos = startWheelPos + Vector3.down * (wheelRadius * 2.5f);
        if (endReturnWheel == null)
            endReturnPos = endWheelPos + Vector3.down * (wheelRadius * 2.5f);

        Debug.Log("=== WHEEL POSITIONS ===");
        Debug.Log("Green (Start Wheel): " + startWheelPos);
        Debug.Log("Red (End Wheel): " + endWheelPos);
        Debug.Log("Cyan (Start Return Wheel): " + startReturnPos);
        Debug.Log("Magenta (End Return Wheel): " + endReturnPos);

        // CORRECT LOOP PATH: Green -> Towers (uphill) -> Red -> Cyan -> Towers (downhill) -> Magenta -> Green

        // Sort towers by distance from start (based on uphill wheel position)
        List<SkiLiftTower> sortedTowers = new List<SkiLiftTower>(towers);
        sortedTowers.Sort((a, b) =>
        {
            Vector3 posA = a.uphillWheel != null ? a.uphillWheel.position : Vector3.zero;
            Vector3 posB = b.uphillWheel != null ? b.uphillWheel.position : Vector3.zero;
            return Vector3.Distance(startWheelPos, posA).CompareTo(Vector3.Distance(startWheelPos, posB));
        });

        // Start from green wheel
        cablePoints.Add(startWheelPos);
        Debug.Log("STEP 1: Starting at Green: " + startWheelPos);

        Vector3 lastPoint = startWheelPos;

        // Go through each tower UPHILL wheel with UPHILL sag
        for (int i = 0; i < sortedTowers.Count; i++)
        {
            if (sortedTowers[i].uphillWheel == null)
            {
                Debug.LogWarning("Tower " + i + " has no uphill wheel assigned, skipping");
                continue;
            }

            Vector3 towerPos = sortedTowers[i].uphillWheel.position;
            Debug.Log("STEP 2: Tower " + i + " UPHILL wheel at: " + towerPos);
            AddCatenarySegment(lastPoint, towerPos, uphillCableSag);
            lastPoint = towerPos;
        }

        // Last tower to red wheel (end) with UPHILL sag
        Debug.Log("STEP 3: Tower -> Red: " + endWheelPos);
        AddCatenarySegment(lastPoint, endWheelPos, uphillCableSag);

        // Red to CYAN (straight connection at end station)
        Debug.Log("STEP 4: Red -> Cyan: " + endWheelPos + " to " + startReturnPos);
        AddStraightSegment(endWheelPos, startReturnPos, 10);

        // CYAN to first tower DOWNHILL wheel, then through towers in reverse
        lastPoint = startReturnPos;

        // Reverse tower order for downhill (Cyan is at start, so we go backwards)
        for (int i = sortedTowers.Count - 1; i >= 0; i--)
        {
            if (sortedTowers[i].downhillWheel == null)
            {
                Debug.LogWarning("Tower " + i + " has no downhill wheel assigned, skipping");
                continue;
            }

            Vector3 towerDownhillPos = sortedTowers[i].downhillWheel.position;
            Debug.Log("STEP 5: Tower " + i + " DOWNHILL wheel at: " + towerDownhillPos);
            AddCatenarySegment(lastPoint, towerDownhillPos, downhillCableSag);
            lastPoint = towerDownhillPos;
        }

        // Last downhill tower to MAGENTA with DOWNHILL sag
        Debug.Log("STEP 6: Tower -> Magenta: " + endReturnPos);
        AddCatenarySegment(lastPoint, endReturnPos, downhillCableSag);

        // MAGENTA back to Green (straight connection at start station - closes loop)
        Debug.Log("STEP 7: Magenta -> Green: " + endReturnPos + " to " + startWheelPos);
        AddStraightSegment(endReturnPos, startWheelPos, 10);

        CalculateTotalLength();

        Debug.Log("=== CABLE COMPLETE ===");
        Debug.Log("Total points: " + cablePoints.Count + ", Total length: " + totalCableLength);
        Debug.Log("First point: " + cablePoints[0]);
        Debug.Log("Last point: " + cablePoints[cablePoints.Count - 1]);
    }

    /// <summary>
    /// Gets the world position of a transform, optionally getting child position
    /// </summary>
    Vector3 GetWorldPosition(Transform t)
    {
        if (t == null)
            return Vector3.zero;

        // If wheelChildIndex is -1, use the parent's position
        if (wheelChildIndex < 0)
            return t.position;

        // Try to get the child at the specified index
        if (t.childCount > wheelChildIndex)
        {
            Transform child = t.GetChild(wheelChildIndex);
            return child.position;
        }

        // Fallback to parent position if child doesn't exist
        return t.position;
    }

    void AddWheelArc(Vector3 center, float radius, float startAngle, float endAngle)
    {
        int segments = 30;
        float angleStep = (endAngle - startAngle) / segments;

        for (int i = 0; i <= segments; i++)
        {
            float angle = (startAngle + angleStep * i) * Mathf.Deg2Rad;
            Vector3 point = center + new Vector3(0f, Mathf.Sin(angle), Mathf.Cos(angle)) * radius;
            cablePoints.Add(point);
        }
    }

    void AddStraightSegment(Vector3 start, Vector3 end, int segments)
    {
        // Skip the first point if it's the same as the last point already added
        int startIndex = 0;
        if (cablePoints.Count > 0 && Vector3.Distance(cablePoints[cablePoints.Count - 1], start) < 0.01f)
        {
            startIndex = 1;
        }

        for (int i = startIndex; i <= segments; i++)
        {
            float t = i / (float)segments;
            cablePoints.Add(Vector3.Lerp(start, end, t));
        }
    }

    void AddCatenarySegment(Vector3 start, Vector3 end, float sagPercentage)
    {
        int segments = curveSegments;
        float distance = Vector3.Distance(start, end);

        if (distance < 0.01f)
        {
            Debug.LogWarning("AddCatenarySegment: Start and end are too close, skipping");
            return;
        }

        float sag = distance * sagPercentage;

        // Skip the first point if it's the same as the last point already added
        int startIndex = 0;
        if (cablePoints.Count > 0 && Vector3.Distance(cablePoints[cablePoints.Count - 1], start) < 0.01f)
        {
            startIndex = 1;
        }

        for (int i = startIndex; i <= segments; i++)
        {
            float t = i / (float)segments;
            Vector3 point = Vector3.Lerp(start, end, t);
            float sagAmount = 4f * sag * t * (1f - t);
            point.y -= sagAmount;
            cablePoints.Add(point);
        }
    }

    void CalculateTotalLength()
    {
        totalCableLength = 0f;

        if (cablePoints.Count < 2)
        {
            Debug.LogError("Not enough cable points to calculate length!");
            return;
        }

        for (int i = 1; i < cablePoints.Count; i++)
        {
            float segmentLength = Vector3.Distance(cablePoints[i - 1], cablePoints[i]);

            if (float.IsNaN(segmentLength) || float.IsInfinity(segmentLength))
            {
                Debug.LogError("Invalid cable segment at index " + i + ": " + cablePoints[i - 1] + " to " + cablePoints[i]);
                continue;
            }

            totalCableLength += segmentLength;
        }

        if (totalCableLength <= 0f || float.IsNaN(totalCableLength))
        {
            Debug.LogError("Total cable length is invalid: " + totalCableLength);
            totalCableLength = 1f; // Prevent division by zero
        }
    }

    void CreateCableLineRenderer()
    {
        GameObject cableObj = new GameObject("CableLineRenderer");
        cableObj.transform.SetParent(transform, false);
        cableObj.transform.localPosition = Vector3.zero;
        cableObj.transform.localRotation = Quaternion.identity;
        cableObj.transform.localScale = Vector3.one;

        cableRenderer = cableObj.AddComponent<LineRenderer>();

        // Set line renderer to use world space positions
        cableRenderer.useWorldSpace = true;

        // Unity 2017 LineRenderer setup
#if UNITY_2017
        cableRenderer.SetVertexCount(cablePoints.Count);
        cableRenderer.SetWidth(cableWidth, cableWidth);
        for (int i = 0; i < cablePoints.Count; i++)
        {
            cableRenderer.SetPosition(i, cablePoints[i]);
        }
        cableRenderer.castShadows = true;
        cableRenderer.receiveShadows = true;
#else
        cableRenderer.positionCount = cablePoints.Count;
        cableRenderer.startWidth = cableWidth;
        cableRenderer.endWidth = cableWidth;
        cableRenderer.SetPositions(cablePoints.ToArray());
        cableRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
        cableRenderer.receiveShadows = true;
#endif

        // Create or assign material with proper shader
        if (cableMaterial != null)
        {
            // Clone to avoid modifying the original
            Material clonedMat = new Material(cableMaterial);

            // Force change shader if it's GUI shader
            if (clonedMat.shader.name.Contains("GUI"))
            {
                Debug.LogWarning("Cable material has GUI shader! Changing to Diffuse.");
                Shader diffuseShader = Shader.Find("Diffuse");
                if (diffuseShader != null)
                {
                    clonedMat.shader = diffuseShader;
                }
            }

            cableRenderer.material = clonedMat;
            Debug.Log("Using cable material with shader: " + cableRenderer.material.shader.name);
        }
        else
        {
            // Create new material with proper shader
            Shader shader = Shader.Find("Diffuse");
            if (shader == null)
                shader = Shader.Find("Standard");
            if (shader == null)
                shader = Shader.Find("Legacy Shaders/Diffuse");

            if (shader != null)
            {
                Material mat = new Material(shader);
                mat.color = new Color(0.3f, 0.3f, 0.3f);
                cableRenderer.material = mat;
                Debug.Log("Created cable material with shader: " + shader.name);
            }
            else
            {
                Debug.LogError("No suitable shader found for cable!");
            }
        }

        Debug.Log("Cable LineRenderer created at world space with " + cablePoints.Count + " points");
        Debug.Log("First cable point: " + cablePoints[0]);
        Debug.Log("Last cable point: " + cablePoints[cablePoints.Count - 1]);
    }

    /// <summary>
    /// Creates visible debug spheres that show in Game view during runtime
    /// </summary>
    void CreateRuntimeDebugVisualization()
    {
        if (!showRuntimeDebug)
            return;

        // Clear any existing debug spheres
        foreach (GameObject sphere in debugSpheres)
        {
            if (sphere != null)
                Destroy(sphere);
        }
        debugSpheres.Clear();

        GameObject debugContainer = new GameObject("DebugVisualization");
        debugContainer.transform.SetParent(transform, false);

        // Create spheres at wheel positions
        CreateDebugSphere(GetWorldPosition(startWheel), Color.green, 0.5f, "StartWheel", debugContainer.transform);
        CreateDebugSphere(GetWorldPosition(endWheel), Color.red, 0.5f, "EndWheel", debugContainer.transform);

        if (startReturnWheel != null)
            CreateDebugSphere(GetWorldPosition(startReturnWheel), Color.cyan, 0.5f, "StartReturnWheel", debugContainer.transform);
        if (endReturnWheel != null)
            CreateDebugSphere(GetWorldPosition(endReturnWheel), Color.magenta, 0.5f, "EndReturnWheel", debugContainer.transform);

        // Create spheres at tower wheel positions
        for (int i = 0; i < towers.Count; i++)
        {
            if (towers[i].uphillWheel != null)
                CreateDebugSphere(towers[i].uphillWheel.position, Color.yellow, 0.3f, "Tower" + i + "_Uphill", debugContainer.transform);
            if (towers[i].downhillWheel != null)
                CreateDebugSphere(towers[i].downhillWheel.position, new Color(1f, 0.5f, 0f), 0.3f, "Tower" + i + "_Downhill", debugContainer.transform);
        }

        // Create small spheres along cable path (every 10th point to avoid too many)
        for (int i = 0; i < cablePoints.Count; i += 10)
        {
            CreateDebugSphere(cablePoints[i], Color.white, 0.15f, "CablePoint_" + i, debugContainer.transform);
        }

        Debug.Log("Created " + debugSpheres.Count + " debug visualization spheres");
    }

    void CreateDebugSphere(Vector3 position, Color color, float size, string name, Transform parent)
    {
        GameObject sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        sphere.name = name;
        sphere.transform.position = position;
        sphere.transform.localScale = Vector3.one * size;
        sphere.transform.SetParent(parent, true);

        // Remove collider
        Collider col = sphere.GetComponent<Collider>();
        if (col != null)
            Destroy(col);

        // Set color
        Renderer renderer = sphere.GetComponent<Renderer>();
        if (renderer != null)
        {
            Material mat = new Material(Shader.Find("Diffuse"));
            mat.color = color;
            renderer.material = mat;
        }

        debugSpheres.Add(sphere);
    }

    void SpawnChairs()
    {
        float spacing = totalCableLength / chairCount;

        for (int i = 0; i < chairCount; i++)
        {
            GameObject chair = Instantiate(chairPrefab);
            chair.transform.SetParent(transform, true);
            chair.name = "Chair_" + i;
            chair.transform.localScale = Vector3.one * chairScale;

            chairs.Add(chair);

            float initialDistance = (spacing * i) % totalCableLength;
            chairDistances.Add(initialDistance);

            UpdateChairPosition(i);
        }
    }

    void AnimateChairs()
    {
        for (int i = 0; i < chairs.Count; i++)
        {
            chairDistances[i] = (chairDistances[i] + liftSpeed * Time.deltaTime) % totalCableLength;
            UpdateChairPosition(i);
        }
    }

    void UpdateChairPosition(int chairIndex)
    {
        if (chairIndex < 0 || chairIndex >= chairs.Count)
            return;

        if (cablePoints.Count < 2)
        {
            Debug.LogError("Not enough cable points for chair positioning!");
            return;
        }

        float distance = chairDistances[chairIndex];
        float accumulatedDistance = 0f;

        for (int i = 1; i < cablePoints.Count; i++)
        {
            float segmentLength = Vector3.Distance(cablePoints[i - 1], cablePoints[i]);

            if (float.IsNaN(segmentLength) || float.IsInfinity(segmentLength))
            {
                Debug.LogError("Invalid segment length at index " + i);
                continue;
            }

            if (accumulatedDistance + segmentLength >= distance)
            {
                float t = (distance - accumulatedDistance) / segmentLength;

                if (float.IsNaN(t) || float.IsInfinity(t))
                {
                    Debug.LogError("Invalid t value: " + t);
                    return;
                }

                Vector3 cablePosition = Vector3.Lerp(cablePoints[i - 1], cablePoints[i], t);

                if (float.IsNaN(cablePosition.x) || float.IsNaN(cablePosition.y) || float.IsNaN(cablePosition.z))
                {
                    Debug.LogError("NaN position calculated from points: " + cablePoints[i - 1] + " to " + cablePoints[i] + " with t=" + t);
                    return;
                }

                // Offset chair downward so cable is at the top of the chair
                Vector3 chairPosition = cablePosition + Vector3.down * chairVerticalOffset;
                chairs[chairIndex].transform.position = chairPosition;

                Vector3 direction = (cablePoints[i] - cablePoints[i - 1]).normalized;
                if (direction.magnitude > 0.01f && !float.IsNaN(direction.x))
                {
                    chairs[chairIndex].transform.rotation = Quaternion.LookRotation(direction);
                }

                return;
            }

            accumulatedDistance += segmentLength;
        }

        // If we get here, place at the last point with offset
        chairs[chairIndex].transform.position = cablePoints[cablePoints.Count - 1] + Vector3.down * chairVerticalOffset;
    }

    void UpdateChairScales()
    {
        if (Mathf.Abs(chairScale - previousChairScale) > 0.01f)
        {
            foreach (GameObject chair in chairs)
            {
                chair.transform.localScale = Vector3.one * chairScale;
            }
            previousChairScale = chairScale;
        }
    }

    void AnimateWheels()
    {
        if (startWheel != null)
            startWheel.position = startWheelInitialPos;
        if (endWheel != null)
            endWheel.position = endWheelInitialPos;
        if (startReturnWheel != null)
            startReturnWheel.position = startReturnWheelInitialPos;
        if (endReturnWheel != null)
            endReturnWheel.position = endReturnWheelInitialPos;

        float rotationSpeed = (liftSpeed / (2f * Mathf.PI * wheelRadius)) * 360f;
        wheelRotation += rotationSpeed * Time.deltaTime;

        Quaternion rotation = Quaternion.Euler(0f, 0f, wheelRotation);

        if (startWheelVisual != null)
            startWheelVisual.localRotation = rotation;
        if (endWheelVisual != null)
            endWheelVisual.localRotation = rotation;
        if (startReturnWheelVisual != null)
            startReturnWheelVisual.localRotation = rotation;
        if (endReturnWheelVisual != null)
            endReturnWheelVisual.localRotation = rotation;
    }

    void OnDrawGizmos()
    {
        if (startWheel != null)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(startWheel.position, wheelRadius);
            Gizmos.DrawLine(startWheel.position, startWheel.position + Vector3.up * wheelRadius * 1.5f);
        }

        if (endWheel != null)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(endWheel.position, wheelRadius);
            Gizmos.DrawLine(endWheel.position, endWheel.position + Vector3.up * wheelRadius * 1.5f);
        }

        if (startReturnWheel != null)
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(startReturnWheel.position, wheelRadius);
            Gizmos.DrawLine(startReturnWheel.position, startReturnWheel.position + Vector3.down * wheelRadius * 1.5f);
        }

        if (endReturnWheel != null)
        {
            Gizmos.color = Color.magenta;
            Gizmos.DrawWireSphere(endReturnWheel.position, wheelRadius);
            Gizmos.DrawLine(endReturnWheel.position, endReturnWheel.position + Vector3.down * wheelRadius * 1.5f);
        }

        if (towers != null)
        {
            foreach (SkiLiftTower tower in towers)
            {
                if (tower.uphillWheel != null)
                {
                    Gizmos.color = Color.yellow;
                    Gizmos.DrawWireSphere(tower.uphillWheel.position, 0.5f);
                    Gizmos.DrawLine(tower.uphillWheel.position, tower.uphillWheel.position + Vector3.up * 2f);
                }

                if (tower.downhillWheel != null)
                {
                    Gizmos.color = new Color(1f, 0.5f, 0f); // Orange
                    Gizmos.DrawWireSphere(tower.downhillWheel.position, 0.5f);
                    Gizmos.DrawLine(tower.downhillWheel.position, tower.downhillWheel.position + Vector3.down * 2f);
                }
            }
        }

        if (showDebugSpheres && cablePoints != null && cablePoints.Count > 0)
        {
            Gizmos.color = Color.white;
            foreach (Vector3 point in cablePoints)
            {
                Gizmos.DrawSphere(point, 0.2f);
            }

            // Draw lines between points to visualize cable path
            Gizmos.color = Color.yellow;
            for (int i = 1; i < cablePoints.Count; i++)
            {
                Gizmos.DrawLine(cablePoints[i - 1], cablePoints[i]);
            }
        }
    }
}
