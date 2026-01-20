using UnityEngine;
using System.Collections.Generic;
using ModTool.Interface;

public class SkiLiftController : ModBehaviour
{
	[Header("Cable Settings")]
	[Tooltip("Speed of the cable in meters per second")]
	public float cableSpeed = 2f;

	[Tooltip("Radius of the wheels in meters")]
	public float wheelRadius = 0.5f;

	[Header("References")]
	public LineRenderer cableRenderer;
	public GameObject chairsParent;

	[Header("Debug")]
	[Tooltip("Enable debug logging (disable for better performance)")]
	public bool enableDebugLogs = false;

	private List<Transform> chairs = new List<Transform>();
	private List<float> chairDistances = new List<float>();
	private List<float> chairTopOffsets = new List<float>();
	private List<Transform> uphillWheels = new List<Transform>();
	private List<Transform> downhillWheels = new List<Transform>();
	private List<Transform> terminalWheels = new List<Transform>();
	private List<Quaternion> terminalWheelBaseRotations = new List<Quaternion>();
	private float cableLength = 0f;
	private float currentOffset = 0f;
	private float wheelRotation = 0f;
	private bool isInitialized = false;

	void Awake()
	{
		if (enableDebugLogs) Debug.Log("SkiLiftController: Awake called");
	}

	void Start()
	{
		if (enableDebugLogs) Debug.Log("SkiLiftController: Start called");
		Initialize();
	}

	void OnEnable()
	{
		if (enableDebugLogs) Debug.Log("SkiLiftController: OnEnable called");
		if (!isInitialized)
		{
			Initialize();
		}
	}

	private void Initialize()
	{
		if (enableDebugLogs) Debug.Log("SkiLiftController: Initializing...");

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
				if (enableDebugLogs) Debug.Log("SkiLiftController: Found LineRenderer: " + cableRenderer.name);
			}
		}

		// Find chairs parent if not assigned
		if (chairsParent == null)
		{
			chairsParent = gameObject;
			if (enableDebugLogs) Debug.Log("SkiLiftController: Using this GameObject as chairs parent");
		}

		InitializeChairs();
		InitializeWheels();
		CalculateCableLength();

		if (chairs.Count > 0 && cableLength > 0)
		{
			CalculateInitialChairDistances();
			isInitialized = true;
			if (enableDebugLogs) Debug.Log("SkiLiftController: Initialization complete! Ready to animate.");
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
			return;
		}

		// Move the cable offset
		currentOffset += cableSpeed * Time.deltaTime;

		// Loop the offset when it exceeds cable length
		if (currentOffset >= cableLength)
		{
			currentOffset -= cableLength;
		}

		// Update wheel rotation based on cable speed
		float wheelCircumference = 2f * 3.14159f * wheelRadius;
		float rotationDegrees = (cableSpeed * Time.deltaTime / wheelCircumference) * 360f;
		wheelRotation += rotationDegrees;

		// Update chair positions
		UpdateChairPositions();

		// Update wheel rotations
		UpdateWheelRotations();
	}

	private void InitializeChairs()
	{
		chairs.Clear();
		chairTopOffsets.Clear();

		// Search in chairsParent and all children
		Transform[] allTransforms = chairsParent.GetComponentsInChildren<Transform>();

		foreach (Transform t in allTransforms)
		{
			if (t.name.StartsWith("Chair"))
			{
				chairs.Add(t);

				// Calculate the offset from chair position to its highest point
				Bounds bounds = GetChairBounds(t.gameObject);
				float topOffset = bounds.max.y - t.position.y;
				chairTopOffsets.Add(topOffset);

				if (enableDebugLogs) Debug.Log("SkiLiftController: Found chair: " + t.name + " with top offset: " + topOffset);
			}
		}

		if (enableDebugLogs) Debug.Log("SkiLiftController: Total chairs found = " + chairs.Count);
	}

	private Bounds GetChairBounds(GameObject obj)
	{
		Bounds bounds = new Bounds(obj.transform.position, Vector3.zero);
		Renderer[] renderers = obj.GetComponentsInChildren<Renderer>();

		// Make sure we have renderers
		if (renderers.Length == 0)
		{
			if (enableDebugLogs) Debug.LogWarning("SkiLiftController: No renderers found on chair " + obj.name);
			return bounds;
		}

		// Encapsulate all renderer bounds (this automatically accounts for scale)
		foreach (Renderer renderer in renderers)
		{
			bounds.Encapsulate(renderer.bounds);
		}

		return bounds;
	}

	private void InitializeWheels()
	{
		uphillWheels.Clear();
		downhillWheels.Clear();
		terminalWheels.Clear();
		terminalWheelBaseRotations.Clear();

		// Find all tower and terminal objects
		Transform[] allTransforms = chairsParent.GetComponentsInChildren<Transform>();

		foreach (Transform t in allTransforms)
		{
			// Look for objects with "wheel" in the name (case insensitive)
			if (t.name.ToLower().Contains("wheel"))
			{
				// Check if this wheel belongs to a terminal
				bool isTerminalWheel = false;
				Transform parent = t.parent;
				while (parent != null)
				{
					if (parent.name.ToLower().Contains("terminal"))
					{
						isTerminalWheel = true;
						break;
					}
					parent = parent.parent;
				}

				if (isTerminalWheel)
				{
					terminalWheels.Add(t);
					terminalWheelBaseRotations.Add(t.localRotation);
					if (enableDebugLogs) Debug.Log("SkiLiftController: Found terminal wheel: " + t.name + " with base rotation: " + t.localRotation.eulerAngles);
				}
				else
				{
					// Extract the number from the wheel name (e.g., "Wheel0", "Wheel1")
					string numberStr = "";
					foreach (char c in t.name)
					{
						if (char.IsDigit(c))
						{
							numberStr += c;
						}
					}

					if (!string.IsNullOrEmpty(numberStr))
					{
						int wheelNumber = int.Parse(numberStr);

						// Even numbered wheels are uphill, odd are downhill
						if (wheelNumber % 2 == 0)
						{
							uphillWheels.Add(t);
							if (enableDebugLogs) Debug.Log("SkiLiftController: Found uphill wheel: " + t.name);
						}
						else
						{
							downhillWheels.Add(t);
							if (enableDebugLogs) Debug.Log("SkiLiftController: Found downhill wheel: " + t.name);
						}
					}
				}
			}
		}

		if (enableDebugLogs) Debug.Log("SkiLiftController: Total uphill wheels = " + uphillWheels.Count + ", downhill wheels = " + downhillWheels.Count + ", terminal wheels = " + terminalWheels.Count);
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

		if (enableDebugLogs) Debug.Log("SkiLiftController: Cable length = " + cableLength + " meters, Points = " + cableRenderer.positionCount);
	}

	private void CalculateInitialChairDistances()
	{
		chairDistances.Clear();

		foreach (Transform chair in chairs)
		{
			// Find the closest point on the cable to this chair
			float closestDistance = FindDistanceAlongCable(chair.position);
			chairDistances.Add(closestDistance);
			if (enableDebugLogs) Debug.Log("SkiLiftController: " + chair.name + " at distance " + closestDistance.ToString("F2"));
		}
	}

	private void UpdateWheelRotations()
	{
		// Rotate uphill wheels (forward direction) around X axis
		foreach (Transform wheel in uphillWheels)
		{
			if (wheel == null)
				continue;

			// Rotate the wheel around its local X axis in the forward direction
			wheel.localRotation = Quaternion.Euler(wheelRotation, 0f, 0f);
		}

		// Rotate downhill wheels (reverse direction) around X axis
		foreach (Transform wheel in downhillWheels)
		{
			if (wheel == null)
				continue;

			// Rotate the wheel around its local X axis in the reverse direction
			wheel.localRotation = Quaternion.Euler(-wheelRotation, 0f, 0f);
		}

		// Rotate terminal wheels around their local forward axis while preserving original orientation
		for (int i = 0; i < terminalWheels.Count; i++)
		{
			if (terminalWheels[i] == null)
				continue;

			// Apply rotation around the wheel's local forward axis (Z axis in local space)
			// This makes the wheel spin in place regardless of its orientation
			Quaternion baseRotation = terminalWheelBaseRotations[i];
			Quaternion spinRotation = Quaternion.AngleAxis(wheelRotation, Vector3.forward);
			terminalWheels[i].localRotation = baseRotation * spinRotation;
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
			Vector3 cablePosition = GetPointOnCable(chairDistance);
			Vector3 direction = GetDirectionOnCable(chairDistance);

			// Position the chair so its top point is at the cable position
			Vector3 chairPosition = cablePosition;
			chairPosition.y -= chairTopOffsets[i]; // Lower the chair by the distance from its pivot to its top

			// Update chair position
			chairs[i].position = chairPosition;

			// Keep chairs vertical - only rotate on Y axis based on horizontal direction
			if (direction != Vector3.zero)
			{
				// Zero out Y component to keep chair vertical
				Vector3 horizontalDirection = direction;
				horizontalDirection.y = 0;

				if (horizontalDirection != Vector3.zero)
				{
					chairs[i].rotation = Quaternion.LookRotation(horizontalDirection);
				}
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

		// Draw uphill wheels in blue
		if (uphillWheels != null && uphillWheels.Count > 0)
		{
			Gizmos.color = Color.blue;
			foreach (Transform wheel in uphillWheels)
			{
				if (wheel != null)
				{
					Gizmos.DrawWireSphere(wheel.position, 0.3f);
				}
			}
		}

		// Draw downhill wheels in red
		if (downhillWheels != null && downhillWheels.Count > 0)
		{
			Gizmos.color = Color.red;
			foreach (Transform wheel in downhillWheels)
			{
				if (wheel != null)
				{
					Gizmos.DrawWireSphere(wheel.position, 0.3f);
				}
			}
		}

		// Draw terminal wheels in magenta
		if (terminalWheels != null && terminalWheels.Count > 0)
		{
			Gizmos.color = Color.magenta;
			foreach (Transform wheel in terminalWheels)
			{
				if (wheel != null)
				{
					Gizmos.DrawWireSphere(wheel.position, 0.3f);
				}
			}
		}
	}
}
