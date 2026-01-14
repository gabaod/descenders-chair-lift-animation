using UnityEngine;
using System.Collections.Generic;
using ModTool.Interface;

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
public class SkiLiftSystem : ModBehaviour
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
	[Tooltip("GameObject with LineRenderer component for cable - AUTO-CREATED if not assigned")]
	public GameObject cableObject;

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

	[Tooltip("Container for debug visualization objects - AUTO-CREATED if showRuntimeDebug is true")]
	public GameObject debugContainer;

	[Tooltip("Show runtime debug visualization (visible in Game view)")]
	public bool showRuntimeDebug = false;

	[Header("Pre-created Objects (Auto-Generated)")]
	[Tooltip("Parent container that holds all pre-created chair objects - AUTO-CREATED")]
	public Transform chairContainer;

	// Private variables
	private List<GameObject> debugSpheres = new List<GameObject>();
	private List<GameObject> chairs = new List<GameObject>();
	private List<float> chairDistances = new List<float>();
	private List<Vector3> cablePoints = new List<Vector3>();
	private float totalCableLength = 0f;
	private LineRenderer cableRenderer;
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
		SetupCableLineRenderer();
		SetupRuntimeDebugVisualization();
		SetupChairs();
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

		if (chairContainer == null)
		{
			Debug.LogError("SkiLiftSystem: Chair Container not found! Use 'Generate Chairs' button in inspector before exporting.");
			enabled = false;
			return;
		}

		if (showRuntimeDebug && debugContainer == null)
		{
			Debug.LogWarning("SkiLiftSystem: Debug Container not assigned, runtime debug disabled");
			showRuntimeDebug = false;
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
			startWheelInitialPos = startWheel.position;
		if (endWheel != null)
			endWheelInitialPos = endWheel.position;
		if (startReturnWheel != null)
			startReturnWheelInitialPos = startReturnWheel.position;
		if (endReturnWheel != null)
			endReturnWheelInitialPos = endReturnWheel.position;

		Debug.Log("Stored initial positions - Start: " + startWheelInitialPos + ", End: " + endWheelInitialPos);
	}

	void CreateWheelVisuals()
	{
		Transform actualStartWheel = GetActualWheelTransform(startWheel);
		Transform actualEndWheel = GetActualWheelTransform(endWheel);
		Transform actualStartReturnWheel = GetActualWheelTransform(startReturnWheel);
		Transform actualEndReturnWheel = GetActualWheelTransform(endReturnWheel);

		if (actualStartWheel != null)
			startWheelVisual = FindOrCreateVisualChild(actualStartWheel, "WheelVisual");
		if (actualEndWheel != null)
			endWheelVisual = FindOrCreateVisualChild(actualEndWheel, "WheelVisual");
		if (actualStartReturnWheel != null)
			startReturnWheelVisual = FindOrCreateVisualChild(actualStartReturnWheel, "WheelVisual");
		if (actualEndReturnWheel != null)
			endReturnWheelVisual = FindOrCreateVisualChild(actualEndReturnWheel, "WheelVisual");
	}

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

	Transform FindOrCreateVisualChild(Transform parent, string name)
	{
		Transform visual = parent.Find(name);
		if (visual != null)
		{
			return visual;
		}

		if (parent.childCount > 0)
		{
			visual = parent.GetChild(0);
			visual.name = name;
			return visual;
		}

		Debug.LogWarning("No visual child found for " + parent.name + ", using parent transform");
		return parent;
	}

	void GenerateCablePoints()
	{
		cablePoints.Clear();

		Vector3 startWheelPos = GetWorldPosition(startWheel);
		Vector3 endWheelPos = GetWorldPosition(endWheel);
		Vector3 startReturnPos = GetWorldPosition(startReturnWheel);
		Vector3 endReturnPos = GetWorldPosition(endReturnWheel);

		if (startReturnWheel == null)
			startReturnPos = startWheelPos + Vector3.down * (wheelRadius * 2.5f);
		if (endReturnWheel == null)
			endReturnPos = endWheelPos + Vector3.down * (wheelRadius * 2.5f);

		Debug.Log("=== WHEEL POSITIONS ===");
		Debug.Log("Green (Start Wheel): " + startWheelPos);
		Debug.Log("Red (End Wheel): " + endWheelPos);
		Debug.Log("Cyan (Start Return Wheel): " + startReturnPos);
		Debug.Log("Magenta (End Return Wheel): " + endReturnPos);

		List<SkiLiftTower> sortedTowers = new List<SkiLiftTower>(towers);
		sortedTowers.Sort((a, b) =>
		{
			Vector3 posA = a.uphillWheel != null ? a.uphillWheel.position : Vector3.zero;
			Vector3 posB = b.uphillWheel != null ? b.uphillWheel.position : Vector3.zero;
			return Vector3.Distance(startWheelPos, posA).CompareTo(Vector3.Distance(startWheelPos, posB));
		});

		cablePoints.Add(startWheelPos);
		Debug.Log("STEP 1: Starting at Green: " + startWheelPos);

		Vector3 lastPoint = startWheelPos;

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

		Debug.Log("STEP 3: Tower -> Red: " + endWheelPos);
		AddCatenarySegment(lastPoint, endWheelPos, uphillCableSag);

		Debug.Log("STEP 4: Red -> Cyan: " + endWheelPos + " to " + startReturnPos);
		AddStraightSegment(endWheelPos, startReturnPos, 10);

		lastPoint = startReturnPos;

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

		Debug.Log("STEP 6: Tower -> Magenta: " + endReturnPos);
		AddCatenarySegment(lastPoint, endReturnPos, downhillCableSag);

		Debug.Log("STEP 7: Magenta -> Green: " + endReturnPos + " to " + startWheelPos);
		AddStraightSegment(endReturnPos, startWheelPos, 10);

		CalculateTotalLength();

		Debug.Log("=== CABLE COMPLETE ===");
		Debug.Log("Total points: " + cablePoints.Count + ", Total length: " + totalCableLength);
		Debug.Log("First point: " + cablePoints[0]);
		Debug.Log("Last point: " + cablePoints[cablePoints.Count - 1]);
	}

	Vector3 GetWorldPosition(Transform t)
	{
		if (t == null)
			return Vector3.zero;

		if (wheelChildIndex < 0)
			return t.position;

		if (t.childCount > wheelChildIndex)
		{
			Transform child = t.GetChild(wheelChildIndex);
			return child.position;
		}

		return t.position;
	}

	void AddStraightSegment(Vector3 start, Vector3 end, int segments)
	{
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
			totalCableLength = 1f;
		}
	}

	void SetupCableLineRenderer()
	{
		if (cableObject == null)
		{
			Debug.LogError("Cable object not assigned! Use 'Generate Chairs' button to create it.");
			return;
		}

		cableRenderer = cableObject.GetComponent<LineRenderer>();
		if (cableRenderer == null)
		{
			Debug.LogError("Cable object does not have a LineRenderer component!");
			return;
		}

		cableRenderer.useWorldSpace = true;

		cableRenderer.positionCount = cablePoints.Count;
		cableRenderer.startWidth = cableWidth;
		cableRenderer.endWidth = cableWidth;
		cableRenderer.SetPositions(cablePoints.ToArray());
		cableRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
		cableRenderer.receiveShadows = true;

		if (cableMaterial != null)
		{
			Material clonedMat = new Material(cableMaterial);

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

		Debug.Log("Cable LineRenderer setup with " + cablePoints.Count + " points");
	}

	void SetupRuntimeDebugVisualization()
	{
		if (!showRuntimeDebug || debugContainer == null)
			return;

		debugSpheres.Clear();

		for (int i = 0; i < debugContainer.transform.childCount; i++)
		{
			GameObject child = debugContainer.transform.GetChild(i).gameObject;
			debugSpheres.Add(child);
		}

		int sphereIndex = 0;

		if (sphereIndex < debugSpheres.Count)
			PositionDebugSphere(debugSpheres[sphereIndex++], GetWorldPosition(startWheel), Color.green, 0.5f, "StartWheel");
		if (sphereIndex < debugSpheres.Count)
			PositionDebugSphere(debugSpheres[sphereIndex++], GetWorldPosition(endWheel), Color.red, 0.5f, "EndWheel");

		if (startReturnWheel != null && sphereIndex < debugSpheres.Count)
			PositionDebugSphere(debugSpheres[sphereIndex++], GetWorldPosition(startReturnWheel), Color.cyan, 0.5f, "StartReturnWheel");
		if (endReturnWheel != null && sphereIndex < debugSpheres.Count)
			PositionDebugSphere(debugSpheres[sphereIndex++], GetWorldPosition(endReturnWheel), Color.magenta, 0.5f, "EndReturnWheel");

		for (int i = 0; i < towers.Count && sphereIndex < debugSpheres.Count; i++)
		{
			if (towers[i].uphillWheel != null && sphereIndex < debugSpheres.Count)
				PositionDebugSphere(debugSpheres[sphereIndex++], towers[i].uphillWheel.position, Color.yellow, 0.3f, "Tower" + i + "_Uphill");
			if (towers[i].downhillWheel != null && sphereIndex < debugSpheres.Count)
				PositionDebugSphere(debugSpheres[sphereIndex++], towers[i].downhillWheel.position, new Color(1f, 0.5f, 0f), 0.3f, "Tower" + i + "_Downhill");
		}

		Debug.Log("Setup " + sphereIndex + " debug visualization spheres");
	}

	void PositionDebugSphere(GameObject sphere, Vector3 position, Color color, float size, string name)
	{
		if (sphere == null) return;

		sphere.name = name;
		sphere.transform.position = position;
		sphere.transform.localScale = Vector3.one * size;

		Renderer renderer = sphere.GetComponent<Renderer>();
		if (renderer != null)
		{
			if (renderer.material != null)
			{
				renderer.material.color = color;
			}
		}
	}

	void SetupChairs()
	{
		if (chairContainer == null)
		{
			Debug.LogError("Chair container not assigned!");
			return;
		}

		chairs.Clear();
		chairDistances.Clear();

		int childCount = chairContainer.childCount;

		if (childCount == 0)
		{
			Debug.LogError("Chair container has no children! Use 'Generate Chairs' button in inspector.");
			return;
		}

		float spacing = totalCableLength / Mathf.Min(chairCount, childCount);

		for (int i = 0; i < Mathf.Min(chairCount, childCount); i++)
		{
			GameObject chair = chairContainer.GetChild(i).gameObject;
			chair.name = "Chair_" + i;
			chair.transform.localScale = Vector3.one * chairScale;
			chair.SetActive(true);

			chairs.Add(chair);

			float initialDistance = (spacing * i) % totalCableLength;
			chairDistances.Add(initialDistance);

			UpdateChairPosition(i);
		}

		for (int i = chairCount; i < childCount; i++)
		{
			chairContainer.GetChild(i).gameObject.SetActive(false);
		}

		Debug.Log("Setup " + chairs.Count + " chairs from container");
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
					Gizmos.color = new Color(1f, 0.5f, 0f);
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

			Gizmos.color = Color.yellow;
			for (int i = 1; i < cablePoints.Count; i++)
			{
				Gizmos.DrawLine(cablePoints[i - 1], cablePoints[i]);
			}
		}
	}
}
