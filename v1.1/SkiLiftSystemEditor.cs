using UnityEngine;
using UnityEditor;

[CustomEditor(typeof(SkiLiftSystem))]
public class SkiLiftSystemEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        SkiLiftSystem skiLift = (SkiLiftSystem)target;

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Pre-Export Setup", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("Click 'Generate Chairs & Cable' before exporting your mod. This creates all required GameObjects in the scene.", MessageType.Info);

        if (GUILayout.Button("Generate Chairs & Cable", GUILayout.Height(30)))
        {
            GenerateChairsAndCable(skiLift);
        }

        EditorGUILayout.Space();

        if (GUILayout.Button("Clear Generated Objects", GUILayout.Height(25)))
        {
            ClearGeneratedObjects(skiLift);
        }
    }

    void GenerateChairsAndCable(SkiLiftSystem skiLift)
    {
        if (skiLift.chairPrefab == null)
        {
            EditorUtility.DisplayDialog("Error", "Chair Prefab must be assigned before generating!", "OK");
            return;
        }

        if (skiLift.startWheel == null || skiLift.endWheel == null)
        {
            EditorUtility.DisplayDialog("Error", "Start and End wheels must be assigned!", "OK");
            return;
        }

        // Create chair container if it doesn't exist
        if (skiLift.chairContainer == null)
        {
            GameObject containerObj = new GameObject("ChairContainer");
            containerObj.transform.SetParent(skiLift.transform, false);
            skiLift.chairContainer = containerObj.transform;
            Undo.RegisterCreatedObjectUndo(containerObj, "Create Chair Container");
        }
        else
        {
            // Clear existing chairs
            while (skiLift.chairContainer.childCount > 0)
            {
                DestroyImmediate(skiLift.chairContainer.GetChild(0).gameObject);
            }
        }

        // Create cable object if it doesn't exist
        if (skiLift.cableObject == null)
        {
            GameObject cableObj = new GameObject("CableLineRenderer");
            cableObj.transform.SetParent(skiLift.transform, false);
            cableObj.transform.localPosition = Vector3.zero;
            cableObj.transform.localRotation = Quaternion.identity;
            cableObj.transform.localScale = Vector3.one;
            
            LineRenderer lineRenderer = cableObj.AddComponent<LineRenderer>();
            lineRenderer.useWorldSpace = true;
            lineRenderer.startWidth = skiLift.cableWidth;
            lineRenderer.endWidth = skiLift.cableWidth;
            
            skiLift.cableObject = cableObj;
            Undo.RegisterCreatedObjectUndo(cableObj, "Create Cable Object");
        }

        // Create debug container if needed
        if (skiLift.showRuntimeDebug && skiLift.debugContainer == null)
        {
            GameObject debugObj = new GameObject("DebugVisualization");
            debugObj.transform.SetParent(skiLift.transform, false);
            skiLift.debugContainer = debugObj;
            Undo.RegisterCreatedObjectUndo(debugObj, "Create Debug Container");

            // Create debug spheres
            int debugSphereCount = 4 + (skiLift.towers.Count * 2) + 10; // Wheels + towers + some cable points
            for (int i = 0; i < debugSphereCount; i++)
            {
                GameObject sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                sphere.name = "DebugSphere_" + i;
                sphere.transform.SetParent(debugObj.transform, false);
                
                // Remove collider
                DestroyImmediate(sphere.GetComponent<Collider>());
                
                Undo.RegisterCreatedObjectUndo(sphere, "Create Debug Sphere");
            }
        }

        // Generate chairs
        for (int i = 0; i < skiLift.chairCount; i++)
        {
            GameObject chair = (GameObject)PrefabUtility.InstantiatePrefab(skiLift.chairPrefab);
            chair.name = "Chair_" + i;
            chair.transform.SetParent(skiLift.chairContainer, false);
            chair.transform.localScale = Vector3.one * skiLift.chairScale;
            
            Undo.RegisterCreatedObjectUndo(chair, "Create Chair");
        }

        EditorUtility.SetDirty(skiLift);
        EditorUtility.DisplayDialog("Success", 
            "Generated:\n" +
            "- " + skiLift.chairCount + " chairs\n" +
            "- Cable LineRenderer\n" +
            (skiLift.showRuntimeDebug ? "- Debug visualization\n" : "") +
            "\nYou can now export your mod!", 
            "OK");
    }

    void ClearGeneratedObjects(SkiLiftSystem skiLift)
    {
        if (EditorUtility.DisplayDialog("Clear Generated Objects", 
            "This will delete the chair container, cable object, and debug container. Continue?", 
            "Yes", "Cancel"))
        {
            if (skiLift.chairContainer != null)
            {
                Undo.DestroyObjectImmediate(skiLift.chairContainer.gameObject);
                skiLift.chairContainer = null;
            }

            if (skiLift.cableObject != null)
            {
                Undo.DestroyObjectImmediate(skiLift.cableObject);
                skiLift.cableObject = null;
            }

            if (skiLift.debugContainer != null)
            {
                Undo.DestroyObjectImmediate(skiLift.debugContainer);
                skiLift.debugContainer = null;
            }

            EditorUtility.SetDirty(skiLift);
            Debug.Log("Cleared all generated objects");
        }
    }
}
