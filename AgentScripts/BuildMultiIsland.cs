using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class BuildMultiIsland
{
    private const string EnvironmentName = "FiremanHouse_Environment";
    private const string LayoutName = "MultiIslandLayout";

    public static int Build()
    {
        Scene scene = SceneManager.GetActiveScene();
        GameObject environment = GameObject.Find(EnvironmentName);
        if (environment == null)
            throw new InvalidOperationException($"Missing {EnvironmentName} root.");

        RemovePreviousLayout(environment.transform);

        GameObject house = FindChild(environment.transform, "House");
        GameObject path = FindChild(environment.transform, "Yard/Front_Path");
        GameObject hydrant = FindChild(environment.transform, "Yard/Fire_Hydrant");
        GameObject firstTree = environment.GetComponentsInChildren<Transform>(true)
            .FirstOrDefault(t => t.name == "Tree")?.gameObject;
        GameObject oldGround = FindChild(environment.transform, "Yard/Ground");
        Transform playerStart = FindChild(environment.transform, "Firefighter_Start")?.transform;
        Transform entranceTarget = FindChild(environment.transform, "House_Entrance_Target")?.transform;
        FireSpreadSystem fireSystem = environment.GetComponent<FireSpreadSystem>();

        if (house == null || path == null || hydrant == null || firstTree == null || oldGround == null || fireSystem == null)
            throw new InvalidOperationException("The existing house scene is missing a required prototype object.");

        RemoveTestFireObjects(house);
        EnsureAmbientAudio(environment);
        EnsurePlayerAudioListener(environment);

        oldGround.SetActive(false);
        path.transform.position = new Vector3(0f, -0.04f, -7.35f);
        path.transform.localScale = new Vector3(3.4f, 0.12f, 4.2f);

        Material grass = LoadMaterial("Assets/Materials/Grass.mat");
        Material concrete = LoadMaterial("Assets/Materials/Concrete.mat");
        Material trim = LoadMaterial("Assets/Materials/House_Trim.mat");
        Material hydrantMetal = LoadMaterial("Assets/Materials/Hydrant_Metal.mat");
        Material roofEdge = LoadMaterial("Assets/Materials/Roof_Edge.mat");
        Material water = GetOrCreateWaterMaterial();

        CreateRoofInteriorClosure(house, roofEdge);
        RemoveFireSpawnPoints(house);

        GameObject layout = new GameObject(LayoutName);
        layout.transform.SetParent(environment.transform, false);
        layout.isStatic = true;

        CreateCube("Water_Between_Islands", layout.transform, new Vector3(0f, -0.95f, 0f),
            Vector3.zero, new Vector3(84f, 0.5f, 84f), water);

        const float levelRise = 1.5f;
        Vector3[] centers =
        {
            new Vector3(0f, 0f, 0f),
            new Vector3(1.5f, levelRise * 1f, 24.5f),
            new Vector3(26.5f, levelRise * 2f, 24f),
            new Vector3(26f, levelRise * 3f, 1f),
            new Vector3(25.5f, levelRise * 4f, -25f),
            new Vector3(-1f, levelRise * 5f, -26f),
            new Vector3(-26f, levelRise * 6f, -25f),
            new Vector3(-25f, levelRise * 7f, 1f),
            new Vector3(-24f, levelRise * 8f, 25f)
        };
        string[] labels = { "1", "2", "3", "4", "5", "6", "7", "8", "9" };
        float[] houseScale = { 1f, 0.96f, 1.04f, 0.92f, 1.08f, 0.98f, 1.03f, 0.94f, 1.06f };
        Vector3[] treeOffsets =
        {
            new Vector3(-3.5f, 0f, 7.3f),
            new Vector3(3.5f, 0f, 7.3f),
            new Vector3(0f, 0f, 7.8f),
            new Vector3(-4.5f, 0f, 7f),
            new Vector3(4.5f, 0f, 7f),
            new Vector3(-2.5f, 0f, 7.6f),
            new Vector3(2.5f, 0f, 7.6f),
            new Vector3(-4f, 0f, 7.1f),
            new Vector3(4f, 0f, 7.1f)
        };
        Quaternion[] houseRotations = new Quaternion[centers.Length];
        for (int i = 0; i < centers.Length; i++)
        {
            Vector3 doorTarget = i == 0
                ? centers[i] + Vector3.back * 8.2f
                : centers[i - 1];
            houseRotations[i] = GetDoorFacingRotation(centers[i], doorTarget);
        }

        if (playerStart != null)
            playerStart.position = centers[0] + houseRotations[0] * new Vector3(0f, 1f, -8.2f);
        if (entranceTarget != null)
            entranceTarget.position = centers[0] + houseRotations[0] * new Vector3(0f, 1f, -4.8f);

        MoveExistingTrees(environment.transform, centers[0], houseRotations[0], treeOffsets[0], treeOffsets[1]);
        path.transform.position = centers[0] + houseRotations[0] * new Vector3(0f, -0.04f, -7.35f);
        path.transform.rotation = houseRotations[0];

        var fireSpawnPoints = new List<Transform>(centers.Length);
        GameObject finalHouseInstance = null;

        for (int i = 0; i < centers.Length; i++)
        {
            string islandName = $"Island_{labels[i]}";
            GameObject island = new GameObject(islandName);
            island.transform.SetParent(layout.transform, false);
            island.transform.position = centers[i];

            CreateCylinder("Rock_Shore", island.transform, new Vector3(0f, -0.55f, 0f),
                new Vector3(20f, 0.3f, 20f), concrete);
            CreateCylinder("Grass_Top", island.transform, new Vector3(0f, -0.28f, 0f),
                new Vector3(18f, 0.5f, 18f), grass);

            GameObject houseInstance;
            if (i == 0)
            {
                house.transform.SetParent(island.transform, true);
                house.transform.position = centers[i];
                house.transform.rotation = houseRotations[i];
                house.transform.localScale = Vector3.one * houseScale[i];
                houseInstance = house;
            }
            else
            {
                GameObject clone = UnityEngine.Object.Instantiate(house, island.transform);
                clone.name = $"House_Island_{labels[i]}";
                clone.transform.position = centers[i];
                clone.transform.rotation = houseRotations[i];
                clone.transform.localScale = Vector3.one * houseScale[i];
                RemoveFireSpawnPoints(clone);
                houseInstance = clone;

                GameObject clonedPath = UnityEngine.Object.Instantiate(path, island.transform);
                clonedPath.name = $"Front_Path_Island_{labels[i]}";
                clonedPath.transform.position = centers[i] + houseRotations[i] * new Vector3(0f, -0.04f, -7.35f);
                clonedPath.transform.rotation = houseRotations[i];
                clonedPath.transform.localScale = new Vector3(3.4f, 0.12f, 4.2f);

                GameObject clonedHydrant = UnityEngine.Object.Instantiate(hydrant, island.transform);
                clonedHydrant.name = $"Fire_Hydrant_Island_{labels[i]}";
                clonedHydrant.transform.position = centers[i] + new Vector3(-7f, 0f, -6f);
                clonedHydrant.transform.rotation = Quaternion.identity;
                clonedHydrant.transform.localScale = Vector3.one;

                GameObject clonedTree = UnityEngine.Object.Instantiate(firstTree, island.transform);
                clonedTree.name = $"Tree_Island_{labels[i]}";
                clonedTree.transform.position = centers[i] + houseRotations[i] * treeOffsets[i];
                clonedTree.transform.rotation = Quaternion.identity;
                clonedTree.transform.localScale = Vector3.one * (0.9f + (i % 3) * 0.08f);
            }

            if (i == centers.Length - 1)
                finalHouseInstance = houseInstance;

            int spawnCount = i + 1;
            for (int spawnIndex = 0; spawnIndex < spawnCount; spawnIndex++)
            {
                GameObject spawn = new GameObject($"FireSpawnPoint_{labels[i]}_{spawnIndex + 1:00}");
                spawn.transform.SetParent(houseInstance.transform, false);
                spawn.transform.localPosition = GetFireSpawnLocalPosition(spawnIndex, spawnCount);
                spawn.transform.localRotation = Quaternion.identity;
                fireSpawnPoints.Add(spawn.transform);
            }
        }

        for (int i = 0; i < centers.Length - 1; i++)
        {
            CreateLadder(layout.transform, $"FireLadder_{labels[i]}_{labels[i + 1]}",
                centers[i], centers[i + 1], trim, hydrantMetal);
        }

        SerializedObject fireSerialized = new SerializedObject(fireSystem);
        SerializedProperty spawnProperty = fireSerialized.FindProperty("fireSpawnPoints");
        spawnProperty.arraySize = fireSpawnPoints.Count;
        for (int i = 0; i < fireSpawnPoints.Count; i++)
            spawnProperty.GetArrayElementAtIndex(i).objectReferenceValue = fireSpawnPoints[i];

        SerializedProperty finalHouseProperty = fireSerialized.FindProperty("finalHouse");
        if (finalHouseProperty != null)
            finalHouseProperty.objectReferenceValue = finalHouseInstance == null ? null : finalHouseInstance.transform;

        fireSerialized.FindProperty("spawnAllHouseFiresAtStart").boolValue = true;
        fireSerialized.FindProperty("houseSpreadBounds").vector3Value = new Vector3(5.1f, 2.6f, 4.1f);
        fireSerialized.FindProperty("spreadInterval").floatValue = 0.5f;
        fireSerialized.FindProperty("maxFires").intValue = 72;
        fireSerialized.ApplyModifiedPropertiesWithoutUndo();

        EditorUtility.SetDirty(environment);
        EditorUtility.SetDirty(fireSystem);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        return fireSpawnPoints.Count;
    }

    private static void RemovePreviousLayout(Transform environment)
    {
        Transform oldLayout = environment.Find(LayoutName);
        if (oldLayout != null)
        {
            Transform originalHouse = oldLayout.GetComponentsInChildren<Transform>(true)
                .FirstOrDefault(t => t.name == "House");
            if (originalHouse != null)
            {
                originalHouse.SetParent(environment, true);
                originalHouse.position = Vector3.zero;
                originalHouse.rotation = Quaternion.identity;
                originalHouse.localScale = Vector3.one;
            }
            UnityEngine.Object.DestroyImmediate(oldLayout.gameObject);
        }

        var oldHouses = environment.GetComponentsInChildren<Transform>(true)
            .Where(t => t.name.StartsWith("House_Island_", StringComparison.Ordinal))
            .Select(t => t.gameObject)
            .ToArray();
        foreach (GameObject oldHouse in oldHouses)
            UnityEngine.Object.DestroyImmediate(oldHouse);
    }

    private static void MoveExistingTrees(Transform environment, Vector3 center, Quaternion houseRotation,
        Vector3 firstOffset, Vector3 secondOffset)
    {
        var trees = environment.GetComponentsInChildren<Transform>(true)
            .Where(t => t.name == "Tree")
            .ToArray();
        if (trees.Length > 0)
            trees[0].position = center + houseRotation * firstOffset;
        if (trees.Length > 1)
            trees[1].position = center + houseRotation * secondOffset;
    }

    private static Quaternion GetDoorFacingRotation(Vector3 houseCenter, Vector3 ladderTarget)
    {
        Vector3 directionToLadder = ladderTarget - houseCenter;
        directionToLadder.y = 0f;
        if (directionToLadder.sqrMagnitude < 0.0001f)
            return Quaternion.identity;

        return Quaternion.LookRotation(-directionToLadder.normalized, Vector3.up);
    }

    private static void EnsureAmbientAudio(GameObject environment)
    {
        const string objectName = "Ambient_Fire_City";
        const string clipPath = "Assets/techtheist-exdnbd-224289.mp3";

        GameObject audioObject = FindChild(environment.transform, objectName);
        if (audioObject == null)
        {
            audioObject = new GameObject(objectName);
            audioObject.transform.SetParent(environment.transform, false);
        }

        AudioSource source = audioObject.GetComponent<AudioSource>();
        if (source == null)
            source = audioObject.AddComponent<AudioSource>();

        source.clip = AssetDatabase.LoadAssetAtPath<AudioClip>(clipPath);
        source.loop = true;
        source.playOnAwake = true;
        source.spatialBlend = 0f;
        source.volume = 0.45f;
        source.dopplerLevel = 0f;
        source.enabled = source.clip != null;
        EditorUtility.SetDirty(audioObject);
    }

    private static void EnsurePlayerAudioListener(GameObject environment)
    {
        Camera[] cameras = Resources.FindObjectsOfTypeAll<Camera>()
            .Where(camera => camera.gameObject.scene.IsValid())
            .ToArray();
        Camera playerCamera = cameras.FirstOrDefault(camera => camera.name == "PlayerCamera");

        foreach (Camera camera in cameras)
        {
            if (camera.name == "Main Camera")
                camera.enabled = false;
        }

        if (playerCamera == null)
            return;

        playerCamera.enabled = true;
        if (playerCamera.GetComponent<AudioListener>() == null)
            playerCamera.gameObject.AddComponent<AudioListener>();
        EditorUtility.SetDirty(playerCamera.gameObject);
    }

    private static void RemoveTestFireObjects(GameObject house)
    {
        var testObjects = house.GetComponentsInChildren<Transform>(true)
            .Where(t => t.name == "Fire_Test_Point")
            .Select(t => t.gameObject)
            .ToArray();
        foreach (GameObject testObject in testObjects)
            UnityEngine.Object.DestroyImmediate(testObject);
    }

    private static void RemoveFireSpawnPoints(GameObject house)
    {
        var spawnObjects = house.GetComponentsInChildren<Transform>(true)
            .Where(t => t.name.StartsWith("FireSpawnPoint_", StringComparison.Ordinal))
            .Select(t => t.gameObject)
            .ToArray();
        foreach (GameObject spawnObject in spawnObjects)
            UnityEngine.Object.DestroyImmediate(spawnObject);
    }

    private static Vector3 GetFireSpawnLocalPosition(int index, int count)
    {
        if (count == 1)
            return new Vector3(0f, 1.35f, 0f);

        int columns = Mathf.CeilToInt(Mathf.Sqrt(count));
        int rows = Mathf.CeilToInt(count / (float)columns);
        int column = index % columns;
        int row = index / columns;
        const float spacing = 1.55f;
        float x = (column - (columns - 1) * 0.5f) * spacing;
        float z = (row - (rows - 1) * 0.5f) * spacing;
        return new Vector3(x, 1.35f, z);
    }

    private static void CreateRoofInteriorClosure(GameObject house, Material material)
    {
        var oldPanels = house.GetComponentsInChildren<Transform>(true)
            .Where(t => t.name == "Roof_Interior_Closure_Left" || t.name == "Roof_Interior_Closure_Right")
            .Select(t => t.gameObject)
            .ToArray();
        foreach (GameObject oldPanel in oldPanels)
            UnityEngine.Object.DestroyImmediate(oldPanel);

        const float slopeAngle = 16.37f;
        CreateCube("Roof_Interior_Closure_Left", house.transform,
            new Vector3(-2.725f, 6.77f, 0f), new Vector3(0f, 0f, slopeAngle),
            new Vector3(5.86f, 0.22f, 9.05f), material);
        CreateCube("Roof_Interior_Closure_Right", house.transform,
            new Vector3(2.725f, 6.77f, 0f), new Vector3(0f, 0f, 360f - slopeAngle),
            new Vector3(5.86f, 0.22f, 9.05f), material);
    }

    private static void CreateLadder(Transform parent, string name, Vector3 firstCenter, Vector3 secondCenter,
        Material railMaterial, Material stepMaterial)
    {
        Vector3 horizontal = new Vector3(secondCenter.x - firstCenter.x, 0f, secondCenter.z - firstCenter.z);
        float horizontalDistance = horizontal.magnitude;
        float distance = Vector3.Distance(firstCenter, secondCenter);
        if (horizontalDistance < 0.1f)
            return;

        Vector3 horizontalDirection = horizontal / horizontalDistance;
        Vector3 start = firstCenter + horizontalDirection * 9.05f + Vector3.up * 0.04f;
        Vector3 end = secondCenter - horizontalDirection * 9.05f + Vector3.up * 0.04f;
        Vector3 direction = (end - start).normalized;
        Vector3 midpoint = (start + end) * 0.5f;
        Quaternion ladderRotation = Quaternion.LookRotation(direction, Vector3.up);
        Vector3 ladderEuler = ladderRotation.eulerAngles;
        float ladderLength = Vector3.Distance(start, end);
        Vector3 side = Vector3.Cross(Vector3.up, horizontalDirection).normalized;

        GameObject group = new GameObject(name);
        group.transform.SetParent(parent, false);

        CreateCube("Walkway", group.transform, midpoint,
            ladderEuler, new Vector3(2.7f, 0.18f, ladderLength), stepMaterial);
        CreateCube("Rail_Left", group.transform, midpoint + Vector3.up * 0.58f + side * 0.95f,
            ladderEuler, new Vector3(0.14f, 0.14f, ladderLength), railMaterial);
        CreateCube("Rail_Right", group.transform, midpoint + Vector3.up * 0.58f - side * 0.95f,
            ladderEuler, new Vector3(0.14f, 0.14f, ladderLength), railMaterial);

        int rungCount = Mathf.Max(3, Mathf.CeilToInt(ladderLength / 0.65f));
        for (int i = 0; i <= rungCount; i++)
        {
            float t = i / (float)rungCount;
            Vector3 position = Vector3.Lerp(start, end, t) + Vector3.up * 0.18f;
            CreateCube($"Rung_{i:00}", group.transform, position,
                ladderEuler, new Vector3(2.15f, 0.12f, 0.14f), railMaterial);
        }
    }

    private static GameObject CreateCube(string name, Transform parent, Vector3 position, Vector3 rotation,
        Vector3 scale, Material material)
    {
        GameObject gameObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
        gameObject.name = name;
        gameObject.transform.SetParent(parent, false);
        gameObject.transform.position = position;
        gameObject.transform.eulerAngles = rotation;
        gameObject.transform.localScale = scale;
        AssignMaterial(gameObject, material);
        gameObject.isStatic = true;
        return gameObject;
    }

    private static GameObject CreateCylinder(string name, Transform parent, Vector3 localPosition,
        Vector3 scale, Material material)
    {
        GameObject gameObject = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        gameObject.name = name;
        gameObject.transform.SetParent(parent, false);
        gameObject.transform.localPosition = localPosition;
        gameObject.transform.localScale = scale;
        Collider oldCollider = gameObject.GetComponent<Collider>();
        if (oldCollider != null)
            UnityEngine.Object.DestroyImmediate(oldCollider);
        gameObject.AddComponent<BoxCollider>();
        AssignMaterial(gameObject, material);
        gameObject.isStatic = true;
        return gameObject;
    }

    private static void AssignMaterial(GameObject gameObject, Material material)
    {
        Renderer renderer = gameObject.GetComponent<Renderer>();
        if (renderer != null && material != null)
            renderer.sharedMaterial = material;
    }

    private static GameObject FindChild(Transform root, string path)
    {
        Transform child = root.Find(path);
        return child == null ? null : child.gameObject;
    }

    private static Material LoadMaterial(string path)
    {
        return AssetDatabase.LoadAssetAtPath<Material>(path);
    }

    private static Material GetOrCreateWaterMaterial()
    {
        const string path = "Assets/Materials/Water_Island.mat";
        Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material != null)
            return material;

        Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
        material = new Material(shader)
        {
            name = "Water_Island",
            color = new Color(0.025f, 0.14f, 0.23f, 1f)
        };
        if (material.HasProperty("_BaseColor"))
            material.SetColor("_BaseColor", material.color);
        if (material.HasProperty("_Color"))
            material.SetColor("_Color", material.color);
        if (material.HasProperty("_Metallic"))
            material.SetFloat("_Metallic", 0.25f);
        if (material.HasProperty("_Smoothness"))
            material.SetFloat("_Smoothness", 0.75f);
        AssetDatabase.CreateAsset(material, path);
        return material;
    }
}
