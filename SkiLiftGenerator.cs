using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

public class SkiLiftGenerator : EditorWindow
{
	// Gizmo bounds (only X and Z matter, Y is determined by terrain)
	private Vector2 startPoint = Vector2.zero;
	private Vector2 endPoint = new Vector2(100f, 100f);

	// Chair settings
	private GameObject chairPrefab;
	private int chairCount = 80;
	private float chairYOffset = 0f;
	private float chairScale = 1f;
	private Vector3 chairRotation = Vector3.zero;

	// Tower settings
	private GameObject towerPrefab;
	private int towerCount = 10;
	private float towerScale = 1f;
	private Vector3 towerRotation = new Vector3(0f, 0f, 0f);

	// Terminal settings
	private bool useTerminals = false;
	private GameObject terminal0Prefab;
	private GameObject terminal1Prefab;
	private float terminal0Scale = 1f;
	private float terminal1Scale = 1f;
	private Vector3 terminal0Rotation = new Vector3(0f, 0f, 0f);
	private Vector3 terminal1Rotation = new Vector3(0f, 0f, 0f);

	// Cable settings
	private Material cableMaterial;
	private float cableWidth = 0.3f;

	// Generated objects
	private GameObject skiLiftParent;
	private GameObject existingSkiLift;
	private List<GameObject> towers = new List<GameObject>();
	private List<GameObject> chairs = new List<GameObject>();
	private GameObject terminal0;
	private GameObject terminal1;
	private LineRenderer cableRenderer;

	[MenuItem("Tools/Ski Lift Generator")]
	public static void ShowWindow()
	{
		GetWindow<SkiLiftGenerator>("Ski Lift Generator");
	}

	private void OnEnable()
	{
		SceneView.onSceneGUIDelegate += OnSceneGUI;
		// Repaint to refresh UI after recompile/build
		Repaint();
	}

	private void OnDisable()
	{
		SceneView.onSceneGUIDelegate -= OnSceneGUI;
	}

	private void OnFocus()
	{
		// Repaint when window gains focus
		Repaint();
	}

	private void OnLostFocus()
	{
		// Repaint when window loses focus
		Repaint();
	}

	private void OnGUI()
	{
		GUILayout.Label("Ski Lift Generator", EditorStyles.boldLabel);

		EditorGUILayout.Space();
		GUILayout.Label("Gizmo Bounds (X, Z only)", EditorStyles.boldLabel);
		startPoint = EditorGUILayout.Vector2Field("Start Point (X, Z)", startPoint);
		endPoint = EditorGUILayout.Vector2Field("End Point (X, Z)", endPoint);

		if (GUILayout.Button("Set Start to Scene View Center"))
		{
			SceneView sceneView = SceneView.lastActiveSceneView;
			if (sceneView != null)
			{
				Vector3 pos = sceneView.camera.transform.position;
				startPoint = new Vector2(pos.x, pos.z);
			}
		}

		if (GUILayout.Button("Set End to Scene View Center"))
		{
			SceneView sceneView = SceneView.lastActiveSceneView;
			if (sceneView != null)
			{
				Vector3 pos = sceneView.camera.transform.position;
				endPoint = new Vector2(pos.x, pos.z);
			}
		}

		EditorGUILayout.Space();
		GUILayout.Label("Chair Settings", EditorStyles.boldLabel);
		chairPrefab = (GameObject)EditorGUILayout.ObjectField("Chair Prefab", chairPrefab, typeof(GameObject), false);
		chairCount = EditorGUILayout.IntField("Chair Count", chairCount);
		chairYOffset = EditorGUILayout.FloatField("Chair Y Offset", chairYOffset);
		chairScale = EditorGUILayout.FloatField("Chair Scale", chairScale);
		chairRotation = EditorGUILayout.Vector3Field("Chair Rotation", chairRotation);

		EditorGUILayout.Space();
		GUILayout.Label("Tower Settings", EditorStyles.boldLabel);
		towerPrefab = (GameObject)EditorGUILayout.ObjectField("Tower Prefab", towerPrefab, typeof(GameObject), false);
		towerCount = EditorGUILayout.IntField("Tower Count", towerCount);
		towerScale = EditorGUILayout.FloatField("Tower Scale", towerScale);
		towerRotation = EditorGUILayout.Vector3Field("Tower Rotation", towerRotation);

		EditorGUILayout.Space();
		GUILayout.Label("Terminal Settings", EditorStyles.boldLabel);
		useTerminals = EditorGUILayout.Toggle("Use Terminals", useTerminals);

		if (useTerminals)
		{
			EditorGUI.indentLevel++;
			terminal0Prefab = (GameObject)EditorGUILayout.ObjectField("Terminal 0 Prefab (Required)", terminal0Prefab, typeof(GameObject), false);
			terminal0Scale = EditorGUILayout.FloatField("Terminal 0 Scale", terminal0Scale);
			terminal0Rotation = EditorGUILayout.Vector3Field("Terminal 0 Rotation", terminal0Rotation);
			terminal1Prefab = (GameObject)EditorGUILayout.ObjectField("Terminal 1 Prefab (Optional)", terminal1Prefab, typeof(GameObject), false);
			if (terminal1Prefab != null)
			{
				terminal1Scale = EditorGUILayout.FloatField("Terminal 1 Scale", terminal1Scale);
				terminal1Rotation = EditorGUILayout.Vector3Field("Terminal 1 Rotation", terminal1Rotation);
			}
			EditorGUI.indentLevel--;
		}

		EditorGUILayout.Space();
		GUILayout.Label("Cable Settings", EditorStyles.boldLabel);
		cableMaterial = (Material)EditorGUILayout.ObjectField("Cable Material", cableMaterial, typeof(Material), false);
		cableWidth = EditorGUILayout.FloatField("Cable Width", cableWidth);

		EditorGUILayout.Space();
		if (GUILayout.Button("Generate Ski Lift"))
		{
			GenerateSkiLift();
		}

		if (GUILayout.Button("Clear Ski Lift"))
		{
			ClearSkiLift();
		}

		EditorGUILayout.Space();
		GUILayout.Label("Regenerate Cable & Chairs Only", EditorStyles.boldLabel);
		existingSkiLift = (GameObject)EditorGUILayout.ObjectField("Existing SkiLift Parent", existingSkiLift, typeof(GameObject), true);

		if (GUILayout.Button("Regenerate Cable & Chairs"))
		{
			RegenerateCableAndChairs();
		}

		EditorGUILayout.Space();
		EditorGUILayout.HelpBox("Y coordinates are automatically determined by terrain height.", MessageType.Info);

		SceneView.RepaintAll();
	}

	private void OnSceneGUI(SceneView sceneView)
	{
		// Draw gizmo box on terrain surface
		Handles.color = Color.cyan;

		// Get terrain heights for the four corners
		Vector3 corner1 = new Vector3(startPoint.x, GetTerrainHeight(startPoint.x, startPoint.y), startPoint.y);
		Vector3 corner2 = new Vector3(endPoint.x, GetTerrainHeight(endPoint.x, startPoint.y), startPoint.y);
		Vector3 corner3 = new Vector3(endPoint.x, GetTerrainHeight(endPoint.x, endPoint.y), endPoint.y);
		Vector3 corner4 = new Vector3(startPoint.x, GetTerrainHeight(startPoint.x, endPoint.y), endPoint.y);

		// Draw the box on the terrain
		Handles.DrawLine(corner1, corner2);
		Handles.DrawLine(corner2, corner3);
		Handles.DrawLine(corner3, corner4);
		Handles.DrawLine(corner4, corner1);

		// Draw vertical lines to show volume (10 units tall)
		Handles.DrawLine(corner1, corner1 + Vector3.up * 10f);
		Handles.DrawLine(corner2, corner2 + Vector3.up * 10f);
		Handles.DrawLine(corner3, corner3 + Vector3.up * 10f);
		Handles.DrawLine(corner4, corner4 + Vector3.up * 10f);

		// Draw top of box
		Vector3 top1 = corner1 + Vector3.up * 10f;
		Vector3 top2 = corner2 + Vector3.up * 10f;
		Vector3 top3 = corner3 + Vector3.up * 10f;
		Vector3 top4 = corner4 + Vector3.up * 10f;

		Handles.DrawLine(top1, top2);
		Handles.DrawLine(top2, top3);
		Handles.DrawLine(top3, top4);
		Handles.DrawLine(top4, top1);
	}

	private float GetTerrainHeight(float x, float z)
	{
		Terrain terrain = Terrain.activeTerrain;
		if (terrain != null)
		{
			return terrain.SampleHeight(new Vector3(x, 0, z));
		}

		// Fallback to raycast
		RaycastHit hit;
		if (Physics.Raycast(new Vector3(x, 5000f, z), Vector3.down, out hit, 10000f))
		{
			return hit.point.y;
		}

		return 0f;
	}

	private void GenerateSkiLift()
	{
		if (chairPrefab == null || towerPrefab == null)
		{
			EditorUtility.DisplayDialog("Error", "Please assign both Chair and Tower prefabs!", "OK");
			return;
		}

		if (useTerminals && terminal0Prefab == null)
		{
			EditorUtility.DisplayDialog("Error", "Terminal 0 is required when Use Terminals is enabled!", "OK");
			return;
		}

		ClearSkiLift();

		skiLiftParent = new GameObject("SkiLift");
		Undo.RegisterCreatedObjectUndo(skiLiftParent, "Create Ski Lift");

		if (useTerminals)
		{
			GenerateTerminals();
		}

		GenerateTowers();
		GenerateCable();
		GenerateChairs();
	}

	private void GenerateTowers()
	{
		towers.Clear();

		// Calculate total distance from start to end
		float totalDistance = Vector2.Distance(startPoint, endPoint);

		// Determine actual start and end points for towers
		Vector2 towerStart = startPoint;
		Vector2 towerEnd = endPoint;

		// Calculate number of segments (terminals to towers, towers to towers, towers to terminal)
		int totalSegments = towerCount + 1; // Default: segments between towers

		if (useTerminals && terminal0Prefab != null)
		{
			totalSegments++; // Add segment from terminal0 to first tower
		}

		if (useTerminals && terminal1Prefab != null)
		{
			totalSegments++; // Add segment from last tower to terminal1
		}

		// Calculate segment length
		float segmentLength = totalDistance / totalSegments;

		// Calculate tower start position
		if (useTerminals && terminal0Prefab != null)
		{
			Vector2 direction = (endPoint - startPoint).normalized;
			towerStart = startPoint + direction * segmentLength;
		}

		// Calculate tower end position
		if (useTerminals && terminal1Prefab != null)
		{
			Vector2 direction = (endPoint - startPoint).normalized;
			towerEnd = endPoint - direction * segmentLength;
		}

		// Generate towers
		for (int i = 0; i < towerCount; i++)
		{
			float t = i / (float)(towerCount - 1);
			Vector2 pos2D = Vector2.Lerp(towerStart, towerEnd, t);

			// Get terrain height at this position
			float terrainHeight = GetTerrainHeight(pos2D.x, pos2D.y);
			Vector3 position = new Vector3(pos2D.x, terrainHeight, pos2D.y);

			GameObject tower = Instantiate(towerPrefab, position, Quaternion.identity);
			tower.name = "Tower" + i;
			tower.transform.parent = skiLiftParent.transform;

			// Apply base rotation and scale
			tower.transform.localRotation = Quaternion.Euler(towerRotation);
			tower.transform.localScale = Vector3.one * towerScale;

			// Adjust position so bottom of tower is on terrain
			Bounds bounds = GetBounds(tower);
			if (bounds.size != Vector3.zero)
			{
				tower.transform.position += Vector3.up * bounds.extents.y;
			}

			// Add directional rotation on Y axis
			if (i < towerCount - 1)
			{
				Vector2 nextPos2D = Vector2.Lerp(towerStart, towerEnd, (i + 1) / (float)(towerCount - 1));
				Vector3 lookDir = new Vector3(nextPos2D.x - pos2D.x, 0, nextPos2D.y - pos2D.y);
				if (lookDir != Vector3.zero)
				{
					float yaw = Quaternion.LookRotation(lookDir).eulerAngles.y;
					tower.transform.rotation = Quaternion.Euler(towerRotation.x, yaw + towerRotation.y, towerRotation.z);
				}
			}
			else if (towers.Count > 0)
			{
				tower.transform.rotation = towers[towers.Count - 1].transform.rotation;
			}

			// Add mesh colliders to all child objects
			AddMeshCollidersToChildren(tower);

			towers.Add(tower);
		}

		// Focus scene view on the ski lift
		if (towers.Count > 0)
		{
			Selection.activeGameObject = skiLiftParent;
			SceneView.lastActiveSceneView.FrameSelected();
		}
	}

	private void GenerateTerminals()
	{
		// Generate Terminal 0 (before first tower)
		if (terminal0Prefab != null)
		{
			float terrainHeight = GetTerrainHeight(startPoint.x, startPoint.y);
			Vector3 position = new Vector3(startPoint.x, terrainHeight, startPoint.y);

			terminal0 = Instantiate(terminal0Prefab, position, Quaternion.identity);
			terminal0.name = "Terminal0";
			terminal0.transform.parent = skiLiftParent.transform;
			terminal0.transform.localRotation = Quaternion.Euler(terminal0Rotation);
			terminal0.transform.localScale = Vector3.one * terminal0Scale;

			// Adjust position so bottom is on terrain
			Bounds bounds = GetBounds(terminal0);
			if (bounds.size != Vector3.zero)
			{
				float bottomY = bounds.min.y;
				float currentY = terminal0.transform.position.y;
				terminal0.transform.position = new Vector3(terminal0.transform.position.x, currentY + (terrainHeight - bottomY), terminal0.transform.position.z);
			}

			// Add directional rotation on Y axis
			Vector3 lookDir = new Vector3(endPoint.x - startPoint.x, 0, endPoint.y - startPoint.y);
			if (lookDir != Vector3.zero)
			{
				float yaw = Quaternion.LookRotation(lookDir).eulerAngles.y;
				terminal0.transform.rotation = Quaternion.Euler(terminal0Rotation.x, yaw + terminal0Rotation.y, terminal0Rotation.z);
			}

			// Add mesh colliders to all child objects
			AddMeshCollidersToChildren(terminal0);
		}

		// Generate Terminal 1 (after last tower)
		if (terminal1Prefab != null)
		{
			float terrainHeight = GetTerrainHeight(endPoint.x, endPoint.y);
			Vector3 position = new Vector3(endPoint.x, terrainHeight, endPoint.y);

			terminal1 = Instantiate(terminal1Prefab, position, Quaternion.identity);
			terminal1.name = "Terminal1";
			terminal1.transform.parent = skiLiftParent.transform;
			terminal1.transform.localRotation = Quaternion.Euler(terminal1Rotation);
			terminal1.transform.localScale = Vector3.one * terminal1Scale;

			// Adjust position so bottom is on terrain
			Bounds bounds = GetBounds(terminal1);
			if (bounds.size != Vector3.zero)
			{
				float bottomY = bounds.min.y;
				float currentY = terminal1.transform.position.y;
				terminal1.transform.position = new Vector3(terminal1.transform.position.x, currentY + (terrainHeight - bottomY), terminal1.transform.position.z);
			}

			// Add directional rotation on Y axis
			Vector3 lookDir = new Vector3(startPoint.x - endPoint.x, 0, startPoint.y - endPoint.y);
			if (lookDir != Vector3.zero)
			{
				float yaw = Quaternion.LookRotation(lookDir).eulerAngles.y;
				terminal1.transform.rotation = Quaternion.Euler(terminal1Rotation.x, yaw + terminal1Rotation.y, terminal1Rotation.z);
			}

			// Add mesh colliders to all child objects
			AddMeshCollidersToChildren(terminal1);
		}
	}

	private void GenerateCable()
	{
		GameObject cableObj = new GameObject("Cable");
		cableObj.transform.parent = skiLiftParent.transform;
		cableRenderer = cableObj.AddComponent<LineRenderer>();

		if (cableMaterial != null)
			cableRenderer.material = cableMaterial;

		cableRenderer.startWidth = cableWidth;
		cableRenderer.endWidth = cableWidth;

		List<Vector3> cablePoints = new List<Vector3>();

		// Start with Terminal 0 if it exists
		if (useTerminals && terminal0 != null)
		{
			Transform[] terminal0Wheels = GetWheels(terminal0);
			if (terminal0Wheels.Length > 0)
			{
				List<Vector3> arcPoints = GetTerminalWheelArcPoints(terminal0Wheels[0], true);
				cablePoints.AddRange(arcPoints);
			}
		}

		// Go through towers uphill
		for (int i = 0; i < towers.Count; i++)
		{
			Transform[] wheels = GetWheels(towers[i]);
			List<Transform> uphillWheels = GetUphillWheels(wheels);

			foreach (Transform wheel in uphillWheels)
			{
				Vector3 wheelTop = GetWheelTopPosition(wheel);
				cablePoints.Add(wheelTop);
			}
		}

		// Add Terminal 1 if it exists
		if (useTerminals && terminal1 != null)
		{
			Transform[] terminal1Wheels = GetWheels(terminal1);
			if (terminal1Wheels.Length > 0)
			{
				List<Vector3> arcPoints = GetTerminalWheelArcPoints(terminal1Wheels[0], false);
				cablePoints.AddRange(arcPoints);
			}
		}

		// Go through towers downhill (reverse)
		for (int i = towers.Count - 1; i >= 0; i--)
		{
			Transform[] wheels = GetWheels(towers[i]);
			List<Transform> downhillWheels = GetDownhillWheels(wheels);

			foreach (Transform wheel in downhillWheels)
			{
				Vector3 wheelTop = GetWheelTopPosition(wheel);
				cablePoints.Add(wheelTop);
			}
		}

		// Close the loop back to Terminal 0 or Tower 0
		if (cablePoints.Count > 0)
			cablePoints.Add(cablePoints[0]);

		cableRenderer.positionCount = cablePoints.Count;
		cableRenderer.SetPositions(cablePoints.ToArray());
	}

	private Vector3 GetWheelTopPosition(Transform wheel)
	{
		// Get the wheel's position and add the radius to the Y coordinate
		Vector3 topPosition = wheel.position;

		// Try to get the wheel's actual radius from its mesh or collider
		float wheelRadius = 1f; // Default radius

		// Try to get radius from mesh renderer bounds
		MeshRenderer meshRenderer = wheel.GetComponent<MeshRenderer>();
		if (meshRenderer != null)
		{
			// Use the Y extent of the bounds as the radius
			wheelRadius = meshRenderer.bounds.extents.y;
		}
		else
		{
			// Try to get from collider
			SphereCollider sphereCollider = wheel.GetComponent<SphereCollider>();
			if (sphereCollider != null)
			{
				wheelRadius = sphereCollider.radius * wheel.lossyScale.y;
			}
		}

		// Add the radius to place cable on top
		topPosition.y += wheelRadius;

		return topPosition;
	}

	private List<Vector3> GetTerminalWheelArcPoints(Transform wheel, bool isTerminal0, int pointCount = 8)
	{
		List<Vector3> arcPoints = new List<Vector3>();

		// Get wheel radius
		float wheelRadius = 0.5f;

		MeshRenderer meshRenderer = wheel.GetComponent<MeshRenderer>();
		if (meshRenderer != null)
		{
			// For horizontal wheels, use X or Z extent
			wheelRadius = Mathf.Max(meshRenderer.bounds.extents.x, meshRenderer.bounds.extents.z);
		}
		else
		{
			SphereCollider sphereCollider = wheel.GetComponent<SphereCollider>();
			if (sphereCollider != null)
			{
				wheelRadius = sphereCollider.radius * Mathf.Max(wheel.lossyScale.x, wheel.lossyScale.z);
			}
		}

		Vector3 wheelCenter = wheel.position;

		// Calculate direction from wheel to first/last tower
		Vector3 directionToTower;
		if (isTerminal0 && towers.Count > 0)
		{
			directionToTower = (towers[0].transform.position - wheelCenter).normalized;
		}
		else if (!isTerminal0 && towers.Count > 0)
		{
			directionToTower = (towers[towers.Count - 1].transform.position - wheelCenter).normalized;
		}
		else
		{
			directionToTower = Vector3.forward;
		}

		// Zero out Y component for horizontal direction
		directionToTower.y = 0;
		directionToTower.Normalize();

		// Calculate the angle of the direction to tower
		float baseAngle = Mathf.Atan2(directionToTower.z, directionToTower.x) * Mathf.Rad2Deg;

		// Create arc points around the back half of the wheel (180 degrees)
		// Start from 90 degrees right of tower direction, end at 90 degrees left
		float startAngle = baseAngle + 180f + 90f;
		float endAngle = baseAngle + 180f - 90f;

		for (int i = 0; i <= pointCount; i++)
		{
			float t = i / (float)pointCount;
			float angle = Mathf.Lerp(startAngle, endAngle, t);
			float radians = angle * Mathf.Deg2Rad;

			Vector3 offset = new Vector3(
				Mathf.Cos(radians) * wheelRadius,
				0,
				Mathf.Sin(radians) * wheelRadius
			);

			Vector3 point = wheelCenter + offset;
			arcPoints.Add(point);
		}

		return arcPoints;
	}

	private void GenerateChairs()
	{
		chairs.Clear();

		if (cableRenderer == null || cableRenderer.positionCount < 2)
			return;

		float cableLength = GetCableLength();

		// Calculate spacing between chairs
		float spacing = cableLength / chairCount;

		for (int i = 0; i < chairCount; i++)
		{
			float distance = i * spacing;
			Vector3 cablePosition = GetPointOnCable(distance);
			Vector3 nextPosition = GetPointOnCable(distance + 0.1f);

			// Instantiate chair at cable position with Y offset
			GameObject chair = Instantiate(chairPrefab, cablePosition + Vector3.up * chairYOffset, Quaternion.identity);
			chair.name = "Chair" + i;
			chair.transform.parent = skiLiftParent.transform;
			chair.transform.localScale = Vector3.one * chairScale;

			// Calculate direction of travel (only horizontal component)
			Vector3 lookDir = nextPosition - cablePosition;
			lookDir.y = 0; // Zero out Y to keep chair vertical

			if (lookDir != Vector3.zero)
			{
				// Rotate only around Y axis to face direction of travel
				Quaternion travelRotation = Quaternion.LookRotation(lookDir);
				Quaternion additionalRotation = Quaternion.Euler(chairRotation);
				chair.transform.rotation = travelRotation * additionalRotation;
			}
			else
			{
				chair.transform.rotation = Quaternion.Euler(chairRotation);
			}

			// Add mesh colliders to all child objects
			AddMeshCollidersToChildren(chair);

			chairs.Add(chair);
		}
	}

	private Transform[] GetWheels(GameObject tower)
	{
		List<Transform> wheels = new List<Transform>();
		foreach (Transform child in tower.GetComponentsInChildren<Transform>())
		{
			if (child.name.ToLower().Contains("wheel"))
			{
				wheels.Add(child);
			}
		}
		return wheels.ToArray();
	}

	private List<Transform> GetUphillWheels(Transform[] wheels)
	{
		List<Transform> uphill = new List<Transform>();
		for (int i = 0; i < wheels.Length; i++)
		{
			if (i % 2 == 0) // Even numbered
				uphill.Add(wheels[i]);
		}
		return uphill;
	}

	private List<Transform> GetDownhillWheels(Transform[] wheels)
	{
		List<Transform> downhill = new List<Transform>();
		for (int i = wheels.Length - 1; i >= 0; i--)
		{
			if (i % 2 == 1) // Odd numbered
				downhill.Add(wheels[i]);
		}
		return downhill;
	}

	private float GetCableLength()
	{
		float length = 0f;
		for (int i = 0; i < cableRenderer.positionCount - 1; i++)
		{
			Vector3 p1 = cableRenderer.GetPosition(i);
			Vector3 p2 = cableRenderer.GetPosition(i + 1);
			length += Vector3.Distance(p1, p2);
		}
		return length;
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

	private Bounds GetBounds(GameObject obj)
	{
		Bounds bounds = new Bounds(obj.transform.position, Vector3.zero);
		Renderer[] renderers = obj.GetComponentsInChildren<Renderer>();

		foreach (Renderer renderer in renderers)
		{
			bounds.Encapsulate(renderer.bounds);
		}

		return bounds;
	}

	private void ClearSkiLift()
	{
		if (skiLiftParent != null)
		{
			DestroyImmediate(skiLiftParent);
		}

		towers.Clear();
		chairs.Clear();
		terminal0 = null;
		terminal1 = null;
		cableRenderer = null;
	}

	private void RegenerateCableAndChairs()
	{
		if (existingSkiLift == null)
		{
			EditorUtility.DisplayDialog("Error", "Please assign an existing SkiLift parent object!", "OK");
			return;
		}

		// Set the skiLiftParent to the existing object
		skiLiftParent = existingSkiLift;

		// Find all towers in the existing ski lift
		towers.Clear();
		foreach (Transform child in skiLiftParent.transform)
		{
			if (child.name.StartsWith("Tower"))
			{
				towers.Add(child.gameObject);
			}
		}

		// Sort towers by name to ensure correct order
		towers.Sort((a, b) => {
			int numA = int.Parse(a.name.Replace("Tower", ""));
			int numB = int.Parse(b.name.Replace("Tower", ""));
			return numA.CompareTo(numB);
		});

		// Find terminals if they exist
		terminal0 = null;
		terminal1 = null;
		foreach (Transform child in skiLiftParent.transform)
		{
			if (child.name == "Terminal0")
				terminal0 = child.gameObject;
			if (child.name == "Terminal1")
				terminal1 = child.gameObject;
		}

		// Delete existing cable
		Transform existingCable = skiLiftParent.transform.Find("Cable");
		if (existingCable != null)
		{
			DestroyImmediate(existingCable.gameObject);
		}

		// Delete all existing chairs - use a list to avoid modifying collection while iterating
		List<GameObject> chairsToDelete = new List<GameObject>();
		foreach (Transform child in skiLiftParent.transform)
		{
			if (child.name.StartsWith("Chair"))
			{
				chairsToDelete.Add(child.gameObject);
			}
		}

		// Now delete all collected chairs
		foreach (GameObject chair in chairsToDelete)
		{
			DestroyImmediate(chair);
		}

		chairs.Clear();
		cableRenderer = null;

		// Regenerate cable and chairs
		if (towers.Count == 0)
		{
			EditorUtility.DisplayDialog("Error", "No towers found in the SkiLift object!", "OK");
			return;
		}

		GenerateCable();
		GenerateChairs();

		EditorUtility.DisplayDialog("Success", "Cable and chairs regenerated successfully!", "OK");
	}

	private void AddMeshCollidersToChildren(GameObject parent)
	{
		// Get all MeshFilters in children (excluding the parent itself)
		MeshFilter[] meshFilters = parent.GetComponentsInChildren<MeshFilter>();

		foreach (MeshFilter meshFilter in meshFilters)
		{
			// Skip if this is the parent object
			if (meshFilter.gameObject == parent)
				continue;

			// Skip if mesh filter doesn't have a valid mesh
			if (meshFilter.sharedMesh == null)
				continue;

			// Add MeshCollider if it doesn't already exist
			MeshCollider collider = meshFilter.gameObject.GetComponent<MeshCollider>();
			if (collider == null)
			{
				collider = meshFilter.gameObject.AddComponent<MeshCollider>();
			}

			// Set the mesh and make it non-convex
			collider.sharedMesh = meshFilter.sharedMesh;
			collider.convex = false;
		}
	}
}
