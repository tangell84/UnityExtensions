/* **********************************
 * Branch Spawner, designed for Unity 2019.3
 * - A tool to spawn branch meshes on tree trunks randomly
 *   using raycasts and custom properties
 * - Finalization then compresses the tree and branches
 *   into a single mesh, flattens the material, and LODs it
 *   
 * Author: Tom Angell, April 2020
 * All Rights Reserved
 * **********************************/

using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

[CustomEditor(typeof(BranchSpawner))]
public class BranchSpawnerEditor : Editor
{
    /* 
     * Class to manage the GUI and mechanisms of the branch spawner.
     * NOTE: This is called everytime the Editor is enabled (object selected)
     * Maintain any persistent variables in the MonoBehaviour, not here.
     */
    private BranchSpawner t;

    // Branch-specific properties
    SerializedProperty branchSpawnerProperties, finalizationProperties,
        numberSpawnPositions, spawnDensity, castTopDown,
        branchNeighborCollisionRadiusRaw, branchNeighborCollisionNumber, branchPrefab,
        branchOrientationMode, branchMinScaleRaw, branchMaxScaleRaw, branchMinRotate, branchMaxRotate,
        heightBias, scaleCurve1, scaleCurve2, droopCurve, heightCurve, heightBiasCurve, branchDroop,
        invertBranchDirection;

    // Non-specific branch properties
    SerializedProperty drawSpawnPositions, drawVert, drawDebug,
        LOD_decimationPercents, LOD_decimationModes, LOD_transitionStart, LOD_culled, prefabPath;

    bool showSetup = false, showSpawnProp = true, showDynamics = false, showDebug = false, showFinalization = false;

    private string m_overallSetupMsg = "Setup is incomplete. Please ensure: " +
        "\r\n" +
        "1) that a branch prefab has been assigned, " +
        "\r\n" +
        "2) that the Target Spawn Number is greater than zero, and" +
        "\r\n" +
        "3) that Allowable Collision Neighbors is greater than zero";

    private string m_colliderSetupMsg = "Colliders are not yet setup. " +
        "Branch spawning requires a capsule collider for the trunk, " +
        "and a mesh collider for the overall geometry and branches. " +
        "Press the Generate Colliders button in the Setup section and " +
        "adjust them as necessary.";

    void OnEnable()
    {
        t = (BranchSpawner)target;

        // Initialize, if null
        if (t.Go == null ||
            t.MeshExt == null ||
            t.branchSpawnerProperties == null ||
            t.finalizationProperties == null)
        {
            t.Initialize(t.gameObject);
        }

        // Setup the properties
        drawDebug = serializedObject.FindProperty("drawDebug");
        drawSpawnPositions = serializedObject.FindProperty("drawSpawnPositions");
        drawVert = serializedObject.FindProperty("drawVert");

        // Branch Spawner
        branchSpawnerProperties = serializedObject.FindProperty("branchSpawnerProperties");

        // Note can't use serialized properties in MinMaxSlider
        numberSpawnPositions = branchSpawnerProperties.FindPropertyRelative("numberSpawnPositions");
        spawnDensity = branchSpawnerProperties.FindPropertyRelative("spawnDensity");
        castTopDown = branchSpawnerProperties.FindPropertyRelative("castTopDown");
        branchNeighborCollisionRadiusRaw = branchSpawnerProperties.FindPropertyRelative("branchNeighborCollisionRadiusRaw");
        branchNeighborCollisionNumber = branchSpawnerProperties.FindPropertyRelative("branchNeighborCollisionNumber");
        branchPrefab = branchSpawnerProperties.FindPropertyRelative("branchPrefab");
        branchOrientationMode = branchSpawnerProperties.FindPropertyRelative("branchOrientationMode");
        branchMinScaleRaw = branchSpawnerProperties.FindPropertyRelative("branchMinScaleRaw");
        branchMaxScaleRaw = branchSpawnerProperties.FindPropertyRelative("branchMaxScaleRaw");
        branchMinRotate = branchSpawnerProperties.FindPropertyRelative("branchMinRotate");
        branchMaxRotate = branchSpawnerProperties.FindPropertyRelative("branchMaxRotate");
        heightBias = branchSpawnerProperties.FindPropertyRelative("heightBias");
        scaleCurve1 = branchSpawnerProperties.FindPropertyRelative("scaleCurve1");
        scaleCurve2 = branchSpawnerProperties.FindPropertyRelative("scaleCurve2");
        droopCurve = branchSpawnerProperties.FindPropertyRelative("droopCurve");
        heightCurve = branchSpawnerProperties.FindPropertyRelative("heightCurve");
        heightBiasCurve = branchSpawnerProperties.FindPropertyRelative("heightBiasCurve");
        branchDroop = branchSpawnerProperties.FindPropertyRelative("branchDroop");
        invertBranchDirection = branchSpawnerProperties.FindPropertyRelative("invertBranchDirection");

        // Finalization
        finalizationProperties = serializedObject.FindProperty("finalizationProperties");
        LOD_decimationPercents = finalizationProperties.FindPropertyRelative("LOD_decimationPercents");
        LOD_decimationModes = finalizationProperties.FindPropertyRelative("LOD_decimationModes");
        LOD_transitionStart = finalizationProperties.FindPropertyRelative("LOD_transitionStart");
        LOD_culled = finalizationProperties.FindPropertyRelative("LOD_culled");
        prefabPath = finalizationProperties.FindPropertyRelative("prefabPath");
    }

    public override void OnInspectorGUI()
    {
        // Update object
        serializedObject.Update();

        // Initialize help message
        string layerSetupMsg = string.Format("The tree layer is currently " +
            "set to {0} and the branch layer is set to {1}. Branch spawning requires " +
            "layers be set in order for raycasts to work properly. Please ensure layers " +
            "are assigned for both the tree and branch prefabs.", t.TreeLayerName, t.BranchLayerName);

        // Check for both layers not Nothing or Everything
        var layersReady = t.TreeLayer > 1 && t.BranchLayer > 1;

        // Check for colliders
        var collidersReady = t.gameObject.TryGetComponent<MeshCollider>(out var meshCollider) &&
                t.gameObject.TryGetComponent<CapsuleCollider>(out var capsuleCollider);

        // Setup check
        var setupReady = branchPrefab.objectReferenceValue != null &&
            numberSpawnPositions.intValue > 0 &&
            branchNeighborCollisionNumber.intValue > 0;
            
        // Draw controls
        // Setup
        showSetup = EditorGUILayout.BeginFoldoutHeaderGroup(showSetup, "Setup");
        if (showSetup)
        {
            if (GUILayout.Button("Generate Colliders"))
            {
                t.CreateColliders();
            }
        }
        EditorGUILayout.EndFoldoutHeaderGroup();

        // Spawner
        showSpawnProp = EditorGUILayout.BeginFoldoutHeaderGroup(showSpawnProp, "Branch Spawner");
        if (showSpawnProp)
        {
            EditorGUILayout.LabelField("Branch Spawner");
            EditorGUILayout.PropertyField(branchPrefab, new GUIContent("Branch Prefab"));
            EditorGUILayout.PropertyField(numberSpawnPositions, new GUIContent("Target Spawn Number"));
            EditorGUILayout.CurveField(spawnDensity, Color.red, new Rect(new Vector2(0, 0), new Vector2(1, 1)), new GUIContent("Spawn Density from Trunk"));
            EditorGUILayout.PropertyField(castTopDown, new GUIContent("Cast Top Down?"));
            EditorGUILayout.Slider(branchNeighborCollisionRadiusRaw, 0, 1, new GUIContent("Branch Spawn Collision Radius"));

            // Adjust the radius value by gameObject scale
            t.branchSpawnerProperties.branchNeighborCollisionRadius = branchNeighborCollisionRadiusRaw.floatValue * t.MeshExt.AverageScale;
            EditorGUILayout.IntSlider(branchNeighborCollisionNumber, 0, 10, new GUIContent("Allowable Collision Neighbors"));

            string[] opts = { "Full Custom", "Normal to Trunk", "Conform to Branches" };
            branchOrientationMode.intValue = GUILayout.SelectionGrid(branchOrientationMode.intValue, opts, 1);

            // Adjust the branch scale slider values by gameObject scale
            EditorGUILayout.MinMaxSlider(new GUIContent("Branch Scale Range"), ref t.branchSpawnerProperties.branchMinScaleRaw.x, ref t.branchSpawnerProperties.branchMaxScaleRaw.x, 0, 10);
            t.branchSpawnerProperties.branchMinScale = t.branchSpawnerProperties.branchMinScaleRaw * t.MeshExt.AverageScale;
            t.branchSpawnerProperties.branchMaxScale = t.branchSpawnerProperties.branchMaxScaleRaw * t.MeshExt.AverageScale;
            EditorGUILayout.MinMaxSlider(new GUIContent("Branch Pitch Range"), ref t.branchSpawnerProperties.branchMinRotate.x, ref t.branchSpawnerProperties.branchMaxRotate.x, -60, 60);
            EditorGUILayout.MinMaxSlider(new GUIContent("Branch Yaw Range"), ref t.branchSpawnerProperties.branchMinRotate.y, ref t.branchSpawnerProperties.branchMaxRotate.y, -180, 180);
            EditorGUILayout.MinMaxSlider(new GUIContent("Branch Roll Range"), ref t.branchSpawnerProperties.branchMinRotate.z, ref t.branchSpawnerProperties.branchMaxRotate.z, -90, 90);
            EditorGUILayout.PropertyField(invertBranchDirection, new GUIContent("Invert Branch Direction?"));

            // Display collider message, if necessary
            if (!collidersReady)
                EditorGUILayout.HelpBox(m_colliderSetupMsg, MessageType.Info);

            // Display layer message, if necessary
            if (!layersReady)
                EditorGUILayout.HelpBox(layerSetupMsg, MessageType.Info);

            // Display setup message, if necessary
            if (!setupReady)
                EditorGUILayout.HelpBox(m_overallSetupMsg, MessageType.Info);

            // Allow spawning if all requirements met
            if (collidersReady && layersReady && setupReady)
            {
                if (GUILayout.Button("Determine Spawn Positions"))
                {
                    t.DetermineSpawnPositions(0.05f);
                }
                if (GUILayout.Button("Spawn Branches"))
                {
                    t.SpawnBranches();
                }
            }
        }
        EditorGUILayout.EndFoldoutHeaderGroup();

        // Dynamics
        showDynamics = EditorGUILayout.BeginFoldoutHeaderGroup(showDynamics, "Dynamics");
        if (showDynamics)
        {
            EditorGUILayout.CurveField(scaleCurve1, Color.red, new Rect(new Vector2(0, 0), new Vector2(1, 1)), new GUIContent("Scale Power (height)"));
            EditorGUILayout.CurveField(scaleCurve2, Color.red, new Rect(new Vector2(0, 0), new Vector2(1, 1)), new GUIContent("Scale Power (out branch)"));
            EditorGUILayout.CurveField(droopCurve, Color.red, new Rect(new Vector2(0, -1), new Vector2(1, 2)), new GUIContent("Droop Power"));
            EditorGUILayout.CurveField(heightCurve, Color.red, new Rect(new Vector2(0, 0), new Vector2(1, 1)), new GUIContent("Height Power"));
            EditorGUILayout.PropertyField(heightBias, new GUIContent("Height Bias From Spawn"));
            EditorGUILayout.CurveField(heightBiasCurve, Color.red, new Rect(new Vector2(0, 0), new Vector2(1, 1)), new GUIContent("Height Bias Power"));
            EditorGUILayout.Slider(branchDroop, -1, 1, new GUIContent("Droop"));

            bool propsChange = t.LastDroop != branchDroop.floatValue;
            if (GUILayout.Button("Apply Curves") || propsChange)
            {
                t.AffectBranches();
            }
        }
        EditorGUILayout.EndFoldoutHeaderGroup();

        // Finalization
        showFinalization = EditorGUILayout.BeginFoldoutHeaderGroup(showFinalization, "Finalization");
        if (showFinalization)
        {
            // Iterate through the meshes (materials), and provide decimation parameters for each
            Material[] materials = t.MeshExt.Materials.ToArray();
            for (int i = 0; i < materials.Length; i++)
            {
                Material material = materials[i];
                EditorGUILayout.LabelField("--- Material " + material.name + " ---");
                for (int j = 0; j < 3; j++)
                {
                    int pointer = 0 + (i * 3) + j;
                    // Increase array size if necessary
                    if (pointer >= LOD_decimationPercents.arraySize)
                    {
                        LOD_decimationPercents.arraySize++;
                        LOD_decimationPercents.GetArrayElementAtIndex(pointer).floatValue = (1 - 0.25f * (j + 1));  // an easy initializer 
                    }

                    // Update the percentage value
                    LOD_decimationPercents.GetArrayElementAtIndex(pointer).floatValue = 
                        EditorGUILayout.Slider(new GUIContent("Decimate Percent LOD " + (j + 1)),
                        LOD_decimationPercents.GetArrayElementAtIndex(pointer).floatValue, 1f, 0f);
                }

                // Increase array size if necessary
                if (i >= LOD_decimationModes.arraySize) { LOD_decimationModes.arraySize++; }

                // Update the mode value
                LOD_decimationModes.GetArrayElementAtIndex(i).intValue = 
                    EditorGUILayout.IntSlider(new GUIContent("LOD Decimation Mode (0 = smallest edge, 1 = distributed)"),
                    LOD_decimationModes.GetArrayElementAtIndex(i).intValue, 0, 1);
            }

            EditorGUILayout.Slider(LOD_transitionStart, 1f, 0f, new GUIContent("Start Transition"));
            EditorGUILayout.Slider(LOD_culled, 1f, 0f, new GUIContent("Culled"));
            EditorGUILayout.LabelField("Prefab will generate in the Assets folder, " +
                "with the path/name provided. Example: 'Prefabs/Object'");
            EditorGUILayout.PropertyField(prefabPath, new GUIContent("Prefab Path"));
            if (GUILayout.Button("Finalize"))
            {
                if (prefabPath.stringValue != null && prefabPath.stringValue != "")
                {
                    GameObject newGo = t.Finalize();
                    AssetDatabaseTools.MakePrefab(newGo, prefabPath.stringValue, true);
                }
                else
                {
                    Debug.Log("Prefab path is null. Please enter a prefab path and try again.");
                }
            }
        }
        EditorGUILayout.EndFoldoutHeaderGroup();

        // Debugging
        showDebug = EditorGUILayout.BeginFoldoutHeaderGroup(showDebug, "Debugging");
        if (showDebug)
        {
            EditorGUILayout.PropertyField(drawSpawnPositions, new GUIContent("Draw Spawn Positions?"));
            EditorGUILayout.IntSlider(drawVert, 0, t.SpawnLocations.ToArray().Length, new GUIContent("Draw Spawn Position"));
            EditorGUILayout.LabelField("Positions spawned: " + t.SpawnLocations.ToArray().Length);
            if (drawSpawnPositions.boolValue && drawVert.intValue > 0)
            {
                EditorGUILayout.LabelField("Normalized distance to trunk is: " + t.SpawnLocations.ToArray()[drawVert.intValue-1].NormalizedBranchPosition);
                EditorGUILayout.LabelField("Height fraction is: " + t.SpawnLocations.ToArray()[drawVert.intValue-1].HeightFraction);
            }
            if (t.MostlyTrunk)
            {
                EditorGUILayout.LabelField("This tree is just a trunk.");
            }
        }
        EditorGUILayout.EndFoldoutHeaderGroup();

        // Apply properties
        serializedObject.ApplyModifiedProperties();
    }
    void OnSceneGUI()
    {
        if (t != null)
        {
            if (drawSpawnPositions.boolValue)
                DrawSpawnPositions(drawVert.intValue);
        }
    }

    public void DrawSpawnPositions(int v = 0)
    {
        /* 
        * Just a helper to draw spawn locations
        */

        if (v == 0)  // No draw vert supplied, draw all
        {
            for (int i = 0; i < t.SpawnLocations.ToArray().Length; i++)
            {
                Vector3 vert = t.SpawnLocations[i].Location;
                Handles.DrawLine(vert, vert + Vector3.up);
            }
        }
        else  // Draw vert supplied, draw only it
        {
            Vector3 vert = t.SpawnLocations[v - 1].Location;
            Handles.DrawLine(vert, vert + Vector3.up);
        }
    }
}
