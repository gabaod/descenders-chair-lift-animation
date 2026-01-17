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
	private int chairCount = 20;
	private float chairSpacing = 5f;
	private float chairYOffset = 0f;
	private float chairScale = 1f;

	// Tower settings
	private GameObject towerPrefab;
	private int towerCount = 5;
	private float towerSpacing = 50f;
	private float towerScale = 1f;

	// Cable settings
	private Material cableMaterial;
	private float cableWidth = 0.1f;

	// Generated objects
	private GameObject skiLiftParent;
	private List<GameObject> towers = new List<GameObject>();
	private List<GameObject> chairs = new List<GameObject>();
	private LineRenderer cableRenderer;

	[MenuItem("Tools/Ski Lift Generator")]
	public static void ShowWindow()
	{
		GetWindow<SkiLiftGenerator>("Ski Lift Generator");
	}

	private void OnEnable()
	{
		SceneView.onSceneGUIDelegate += OnSceneGUI;
	}

	private void OnDisable()
	{
		SceneView.onSceneGUIDelegate -= OnSceneGUI;
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
		chairSpacing = EditorGUILayout.FloatField("Chair Spacing (m)", chairSpacing);
		chairYOffset = EditorGUILayout.FloatField("Chair Y Offset", chairYOffset);
		chairScale = EditorGUILayout.FloatField("Chair Scale", chairScale);

		EditorGUILayout.Space();
		GUILayout.Label("Tower Settings", EditorStyles.boldLabel);
		towerPrefab = (GameObject)EditorGUILayout.ObjectField("Tower Prefab", towerPrefab, typeof(GameObject), false);
		towerCount = EditorGUILayout.IntField("Tower Count", towerCount);
		towerSpacing = EditorGUILayout.FloatField("Tower Spacing (m)", towerSpacing);
		towerScale = EditorGUILayout.FloatField("Tower Scale", towerScale);

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

		ClearSkiLift();

		skiLiftParent = new GameObject("SkiLift");
		Undo.RegisterCreatedObjectUndo(skiLiftParent, "Create Ski Lift");

		GenerateTowers();
		GenerateCable();
		GenerateChairs();
	}

	private void GenerateTowers()
	{
		towers.Clear();

		for (int i = 0; i < towerCount; i++)
		{
			float t = i / (float)(towerCount - 1);
			Vector2 pos2D = Vector2.Lerp(startPoint, endPoint, t);

			// Get terrain height at this position
			float terrainHeight = GetTerrainHeight(pos2D.x, pos2D.y);
			Vector3 position = new Vector3(pos2D.x, terrainHeight, pos2D.y);

			GameObject tower = Instantiate(towerPrefab, position, Quaternion.identity);
			tower.name = "Tower" + i;
			tower.transform.parent = skiLiftParent.transform;

			// Rotate tower upright (90 degrees on X axis) and scale
			tower.transform.localRotation = Quaternion.Euler(270f, 0f, 0f);
			tower.transform.localScale = Vector3.one * towerScale;

			// Adjust position so bottom of tower is on terrain
			Bounds bounds = GetBounds(tower);
			if (bounds.size != Vector3.zero)
			{
				tower.transform.position += Vector3.up * bounds.extents.y;
			}

			// Rotate tower to face direction (preserve the upright rotation)
			if (i < towerCount - 1)
			{
				Vector2 nextPos2D = Vector2.Lerp(startPoint, endPoint, (i + 1) / (float)(towerCount - 1));
				Vector3 lookDir = new Vector3(nextPos2D.x - pos2D.x, 0, nextPos2D.y - pos2D.y);
				if (lookDir != Vector3.zero)
				{
					float yaw = Quaternion.LookRotation(lookDir).eulerAngles.y;
					tower.transform.rotation = Quaternion.Euler(270f, yaw, 0f);
				}
			}
			else if (towers.Count > 0)
			{
				tower.transform.rotation = towers[towers.Count - 1].transform.rotation;
			}

			towers.Add(tower);
		}

		// Focus scene view on the ski lift
		if (towers.Count > 0)
		{
			Selection.activeGameObject = skiLiftParent;
			SceneView.lastActiveSceneView.FrameSelected();
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

		// Go through towers uphill
		for (int i = 0; i < towers.Count; i++)
		{
			Transform[] wheels = GetWheels(towers[i]);
			List<Transform> uphillWheels = GetUphillWheels(wheels);

			foreach (Transform wheel in uphillWheels)
			{
				cablePoints.Add(wheel.position);
			}
		}

		// Go through towers downhill (reverse)
		for (int i = towers.Count - 1; i >= 0; i--)
		{
			Transform[] wheels = GetWheels(towers[i]);
			List<Transform> downhillWheels = GetDownhillWheels(wheels);

			foreach (Transform wheel in downhillWheels)
			{
				cablePoints.Add(wheel.position);
			}
		}

		// Close the loop
		if (cablePoints.Count > 0)
			cablePoints.Add(cablePoints[0]);

		cableRenderer.positionCount = cablePoints.Count;
		cableRenderer.SetPositions(cablePoints.ToArray());
	}

	private void GenerateChairs()
	{
		chairs.Clear();

		if (cableRenderer == null || cableRenderer.positionCount < 2)
			return;

		float cableLength = GetCableLength();
		float spacing = cableLength / chairCount;

		for (int i = 0; i < chairCount; i++)
		{
			float distance = i * spacing;
			Vector3 position = GetPointOnCable(distance);
			Vector3 nextPosition = GetPointOnCable(distance + 0.1f);

			GameObject chair = Instantiate(chairPrefab, position + Vector3.up * chairYOffset, Quaternion.identity);
			chair.name = "Chair" + i;
			chair.transform.parent = skiLiftParent.transform;
			chair.transform.localScale = Vector3.one * chairScale;

			// Face direction of travel
			Vector3 lookDir = nextPosition - position;
			if (lookDir != Vector3.zero)
			{
				chair.transform.rotation = Quaternion.LookRotation(lookDir);
			}

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
		cableRenderer = null;
	}
}
