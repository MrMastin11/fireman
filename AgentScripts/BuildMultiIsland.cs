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
        Material wall = LoadMaterial("Assets/Materials/House_Wall.mat");
        Material door = LoadMaterial("Assets/Materials/Door.mat");
        Material glass = LoadMaterial("Assets/Materials/Window_Glass.mat");
        Material roof = LoadMaterial("Assets/Materials/House_Roof.mat");
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

            ConfigureHouseVariant(houseInstance, i + 1, wall, door, glass, roof, trim, concrete);

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

            if (GetHouseVariant(i + 1) == 3)
            {
                CreateUpperLadder(layout.transform, $"FireLadder_Upper_{labels[i]}_{labels[i + 1]}",
                    centers[i], centers[i + 1], houseRotations[i], trim, hydrantMetal);
            }
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

    private static int GetHouseVariant(int sectionNumber)
    {
        if (sectionNumber % 3 == 0)
            return 3;
        if (sectionNumber % 2 == 0)
            return 2;
        return 1;
    }

    private static void ConfigureHouseVariant(GameObject house, int sectionNumber,
        Material wall, Material door, Material glass, Material roof, Material trim, Material concrete)
    {
        switch (GetHouseVariant(sectionNumber))
        {
            case 2:
                ConfigureElongatedHouse(house, wall, door);
                break;
            case 3:
                ConfigureTwoStoryHouse(house, wall, door, glass, roof, trim, concrete);
                break;
        }
    }

    private static void ConfigureElongatedHouse(GameObject house, Material wall, Material door)
    {
        const float depthScale = 1.45f;

        Transform[] children = house.GetComponentsInChildren<Transform>(true);
        foreach (Transform child in children)
        {
            if (child == house.transform)
                continue;

            child.localPosition = new Vector3(
                child.localPosition.x,
                child.localPosition.y,
                child.localPosition.z * depthScale);
            child.localScale = new Vector3(
                child.localScale.x,
                child.localScale.y,
                child.localScale.z * depthScale);
        }

        Transform backWall = house.transform.Find("Back_Wall");
        if (backWall != null)
            backWall.gameObject.SetActive(false);

        float wallZ = 4.25f * depthScale;
        CreateHouseCube("Passage_BackWall_Left", house.transform,
            new Vector3(-3.22f, 3.20f, wallZ), Vector3.zero,
            new Vector3(4.37f, 5.20f, 0.35f), wall);
        CreateHouseCube("Passage_BackWall_Right", house.transform,
            new Vector3(3.22f, 3.20f, wallZ), Vector3.zero,
            new Vector3(4.37f, 5.20f, 0.35f), wall);
        CreateHouseCube("Passage_BackWall_Top", house.transform,
            new Vector3(0f, 4.90f, wallZ), Vector3.zero,
            new Vector3(2.06f, 1.80f, 0.35f), wall);

        float doorZ = 4.48f * depthScale;
        float frameZ = 4.60f * depthScale;
        CreateHouseCube("Passage_Back_Door", house.transform,
            new Vector3(0f, 2.20f, doorZ), Vector3.zero,
            new Vector3(1.65f, 3.20f, 0.18f), door);
        CreateHouseCube("Passage_Back_Frame_Left", house.transform,
            new Vector3(-0.95f, 2.20f, frameZ), Vector3.zero,
            new Vector3(0.16f, 3.60f, 0.16f), door);
        CreateHouseCube("Passage_Back_Frame_Right", house.transform,
            new Vector3(0.95f, 2.20f, frameZ), Vector3.zero,
            new Vector3(0.16f, 3.60f, 0.16f), door);
        CreateHouseCube("Passage_Back_Frame_Top", house.transform,
            new Vector3(0f, 3.92f, frameZ), Vector3.zero,
            new Vector3(2.05f, 0.16f, 0.16f), door);
        CreateHouseCube("Passage_Back_Threshold", house.transform,
            new Vector3(0f, 0.77f, 4.72f * depthScale), Vector3.zero,
            new Vector3(2.15f, 0.16f, 0.30f), door);
    }

    private static void ConfigureTwoStoryHouse(GameObject house, Material wall, Material door,
        Material glass, Material roof, Material trim, Material concrete)
    {
        string[] lowerRoofParts =
        {
            "Roof_Left", "Roof_Right", "Roof_Ridge_Cap", "Roof_Shell",
            "Roof_Interior_Closure_Left", "Roof_Interior_Closure_Right",
            "Front_Gable_Trim_Left", "Front_Gable_Trim_Right",
            "Back_Gable_Trim_Left", "Back_Gable_Trim_Right",
            "Front_Roof_Fascia_Left", "Front_Roof_Fascia_Right",
            "Back_Roof_Fascia_Left", "Back_Roof_Fascia_Right",
            "Front_Gable_Wall", "Back_Gable_Wall", "Chimney", "Chimney_Cap"
        };

        for (int i = 0; i < lowerRoofParts.Length; i++)
        {
            Transform part = house.transform.Find(lowerRoofParts[i]);
            if (part != null)
                part.gameObject.SetActive(false);
        }

        const float upperFloorBase = 5.82f;
        const float upperFloorHeight = 4.50f;
        float upperFloorCenter = upperFloorBase + upperFloorHeight * 0.5f;

        CreateHouseCube("Upper_Floor", house.transform,
            new Vector3(0f, upperFloorBase - 0.12f, 0f), Vector3.zero,
            new Vector3(10.70f, 0.24f, 8.20f), concrete);
        CreateHouseCube("Upper_Front_Wall", house.transform,
            new Vector3(0f, upperFloorCenter, -4.25f), Vector3.zero,
            new Vector3(10.80f, upperFloorHeight, 0.35f), wall);
        CreateHouseCube("Upper_Left_Wall", house.transform,
            new Vector3(-5.25f, upperFloorCenter, 0f), Vector3.zero,
            new Vector3(0.35f, upperFloorHeight, 8.20f), wall);
        CreateHouseCube("Upper_Right_Wall", house.transform,
            new Vector3(5.25f, upperFloorCenter, 0f), Vector3.zero,
            new Vector3(0.35f, upperFloorHeight, 8.20f), wall);

        CreateHouseCube("Upper_BackWall_Left", house.transform,
            new Vector3(-3.22f, upperFloorCenter, 4.25f), Vector3.zero,
            new Vector3(4.37f, upperFloorHeight, 0.35f), wall);
        CreateHouseCube("Upper_BackWall_Right", house.transform,
            new Vector3(3.22f, upperFloorCenter, 4.25f), Vector3.zero,
            new Vector3(4.37f, upperFloorHeight, 0.35f), wall);
        CreateHouseCube("Upper_BackWall_Top", house.transform,
            new Vector3(0f, 9.64f, 4.25f), Vector3.zero,
            new Vector3(2.06f, 1.36f, 0.35f), wall);
        CreateHouseCube("Upper_Back_Door", house.transform,
            new Vector3(0f, 7.42f, 4.48f), Vector3.zero,
            new Vector3(1.65f, 3.20f, 0.18f), door);
        CreateHouseCube("Upper_Back_Frame_Left", house.transform,
            new Vector3(-0.95f, 7.42f, 4.60f), Vector3.zero,
            new Vector3(0.16f, 3.60f, 0.16f), door);
        CreateHouseCube("Upper_Back_Frame_Right", house.transform,
            new Vector3(0.95f, 7.42f, 4.60f), Vector3.zero,
            new Vector3(0.16f, 3.60f, 0.16f), door);
        CreateHouseCube("Upper_Back_Frame_Top", house.transform,
            new Vector3(0f, 9.14f, 4.60f), Vector3.zero,
            new Vector3(2.05f, 0.16f, 0.16f), door);

        CreateHouseCube("Upper_Window_Left", house.transform,
            new Vector3(-3.05f, 8.30f, -4.48f), Vector3.zero,
            new Vector3(1.75f, 1.45f, 0.14f), glass);
        CreateHouseCube("Upper_Window_Right", house.transform,
            new Vector3(3.05f, 8.30f, -4.48f), Vector3.zero,
            new Vector3(1.75f, 1.45f, 0.14f), glass);
        CreateHouseCube("Upper_Window_Sill_Left", house.transform,
            new Vector3(-3.05f, 7.39f, -4.70f), Vector3.zero,
            new Vector3(2.15f, 0.14f, 0.28f), trim);
        CreateHouseCube("Upper_Window_Sill_Right", house.transform,
            new Vector3(3.05f, 7.39f, -4.70f), Vector3.zero,
            new Vector3(2.15f, 0.14f, 0.28f), trim);

        CreateHouseCube("Upper_Roof", house.transform,
            new Vector3(0f, upperFloorBase + upperFloorHeight + 0.22f, 0f), Vector3.zero,
            new Vector3(11.40f, 0.35f, 9.20f), roof);

        const float balconyY = upperFloorBase + 0.10f;
        CreateHouseCube("SecondFloor_Balcony", house.transform,
            new Vector3(0f, balconyY, 5.35f), Vector3.zero,
            new Vector3(4.60f, 0.20f, 1.90f), concrete);
        CreateHouseCube("SecondFloor_Balcony_Rail_Left", house.transform,
            new Vector3(-2.15f, balconyY + 0.62f, 5.35f), Vector3.zero,
            new Vector3(0.14f, 1.20f, 1.90f), trim);
        CreateHouseCube("SecondFloor_Balcony_Rail_Right", house.transform,
            new Vector3(2.15f, balconyY + 0.62f, 5.35f), Vector3.zero,
            new Vector3(0.14f, 1.20f, 1.90f), trim);
        CreateHouseCube("SecondFloor_Balcony_Rail_Back", house.transform,
            new Vector3(0f, balconyY + 0.62f, 6.25f), Vector3.zero,
            new Vector3(4.60f, 1.20f, 0.14f), trim);

        CreateVerticalLadder(house.transform, "SecondFloor_Ladder", new Vector3(4.20f, 3.35f, 4.82f),
            trim, concrete);
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
        if (horizontalDistance < 0.1f)
            return;

        Vector3 horizontalDirection = horizontal / horizontalDistance;
        Vector3 start = firstCenter + horizontalDirection * 9.05f + Vector3.up * 0.04f;
        Vector3 end = secondCenter - horizontalDirection * 9.05f + Vector3.up * 0.04f;
        CreateLadderBetweenPoints(parent, name, start, end, railMaterial, stepMaterial, 2.7f);
    }

    private static void CreateUpperLadder(Transform parent, string name, Vector3 firstCenter,
        Vector3 secondCenter, Quaternion houseRotation, Material railMaterial, Material stepMaterial)
    {
        Vector3 horizontal = new Vector3(secondCenter.x - firstCenter.x, 0f, secondCenter.z - firstCenter.z);
        float horizontalDistance = horizontal.magnitude;
        if (horizontalDistance < 0.1f)
            return;

        Vector3 horizontalDirection = horizontal / horizontalDistance;
        Vector3 start = firstCenter + houseRotation * new Vector3(0f, 5.98f, 6.20f);
        const float landingHeight = 7.25f;
        Vector3 end = secondCenter - horizontalDirection * 9.05f + Vector3.up * landingHeight;
        CreateLadderBetweenPoints(parent, name, start, end, railMaterial, stepMaterial, 2.3f);
        CreateUpperLanding(parent, $"{name}_Landing", secondCenter, horizontalDirection,
            landingHeight, railMaterial, stepMaterial);
    }

    private static void CreateUpperLanding(Transform parent, string name, Vector3 islandCenter,
        Vector3 directionToIsland, float height, Material railMaterial, Material stepMaterial)
    {
        Vector3 center = islandCenter - directionToIsland * 8.65f + Vector3.up * (height - 0.10f);
        Vector3 side = Vector3.Cross(Vector3.up, directionToIsland).normalized;
        GameObject group = new GameObject(name);
        group.transform.SetParent(parent, false);

        CreateCube("Deck", group.transform, center, Vector3.zero,
            new Vector3(2.3f, 0.18f, 2.0f), stepMaterial);
        CreateCube("Rail_Left", group.transform, center + Vector3.up * 0.58f + side * 0.95f,
            Vector3.zero, new Vector3(0.14f, 1.20f, 2.0f), railMaterial);
        CreateCube("Rail_Right", group.transform, center + Vector3.up * 0.58f - side * 0.95f,
            Vector3.zero, new Vector3(0.14f, 1.20f, 2.0f), railMaterial);
    }

    private static void CreateLadderBetweenPoints(Transform parent, string name, Vector3 start, Vector3 end,
        Material railMaterial, Material stepMaterial, float walkwayWidth)
    {
        Vector3 direction = (end - start).normalized;
        Vector3 midpoint = (start + end) * 0.5f;
        Quaternion ladderRotation = Quaternion.LookRotation(direction, Vector3.up);
        Vector3 ladderEuler = ladderRotation.eulerAngles;
        float ladderLength = Vector3.Distance(start, end);
        Vector3 horizontalDirection = new Vector3(end.x - start.x, 0f, end.z - start.z).normalized;
        Vector3 side = Vector3.Cross(Vector3.up, horizontalDirection).normalized;

        GameObject group = new GameObject(name);
        group.transform.SetParent(parent, false);

        CreateCube("Walkway", group.transform, midpoint,
            ladderEuler, new Vector3(walkwayWidth, 0.18f, ladderLength), stepMaterial);
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

    private static void CreateVerticalLadder(Transform parent, string name, Vector3 center,
        Material railMaterial, Material stepMaterial)
    {
        const float height = 5.20f;
        const float railSpacing = 1.70f;
        const float rungWidth = 2.05f;
        float bottom = center.y - height * 0.5f;

        CreateHouseCube($"{name}_Rail_Left", parent,
            new Vector3(center.x - railSpacing * 0.5f, center.y, center.z), Vector3.zero,
            new Vector3(0.14f, height, 0.14f), railMaterial);
        CreateHouseCube($"{name}_Rail_Right", parent,
            new Vector3(center.x + railSpacing * 0.5f, center.y, center.z), Vector3.zero,
            new Vector3(0.14f, height, 0.14f), railMaterial);

        int rungCount = Mathf.Max(5, Mathf.CeilToInt(height / 0.60f));
        for (int i = 0; i <= rungCount; i++)
        {
            float t = i / (float)rungCount;
            CreateHouseCube($"{name}_Rung_{i:00}", parent,
                new Vector3(center.x, bottom + height * t, center.z), Vector3.zero,
                new Vector3(rungWidth, 0.12f, 0.14f), stepMaterial);
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

    private static GameObject CreateHouseCube(string name, Transform parent, Vector3 localPosition,
        Vector3 localRotation, Vector3 localScale, Material material)
    {
        GameObject gameObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
        gameObject.name = name;
        gameObject.transform.SetParent(parent, false);
        gameObject.transform.localPosition = localPosition;
        gameObject.transform.localEulerAngles = localRotation;
        gameObject.transform.localScale = localScale;
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
