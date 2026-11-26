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
        float[] levelHeights = new float[9];
        for (int i = 1; i < levelHeights.Length; i++)
        {
            float rise = GetHouseVariant(i) == 3 ? 6.5f : levelRise;
            levelHeights[i] = levelHeights[i - 1] + rise;
        }

        Vector3[] centers =
        {
            new Vector3(0f, levelHeights[0], 0f),
            new Vector3(1.5f, levelHeights[1], 24.5f),
            new Vector3(26.5f, levelHeights[2], 24f),
            new Vector3(26f, levelHeights[3], 1f),
            new Vector3(25.5f, levelHeights[4], -25f),
            new Vector3(-1f, levelHeights[5], -26f),
            new Vector3(-26f, levelHeights[6], -25f),
            new Vector3(-25f, levelHeights[7], 1f),
            new Vector3(-24f, levelHeights[8], 25f)
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

            Vector3 nextHouseCenter = i < centers.Length - 1
                ? centers[i + 1]
                : centers[i] + houseRotations[i] * Vector3.forward * 20f;
            ConfigureHouseVariant(houseInstance, i + 1, nextHouseCenter,
                wall, door, glass, roof, trim, concrete);

            if (i == centers.Length - 1)
                finalHouseInstance = houseInstance;

            int spawnCount = i + 1;
            bool twoStoryHouse = GetHouseVariant(i + 1) == 3;
            for (int spawnIndex = 0; spawnIndex < spawnCount; spawnIndex++)
            {
                GameObject spawn = new GameObject($"FireSpawnPoint_{labels[i]}_{spawnIndex + 1:00}");
                spawn.transform.SetParent(houseInstance.transform, false);
                spawn.transform.localPosition = GetFireSpawnLocalPosition(spawnIndex, spawnCount, twoStoryHouse);
                spawn.transform.localRotation = Quaternion.identity;
                fireSpawnPoints.Add(spawn.transform);
            }
        }

        for (int i = 0; i < centers.Length - 1; i++)
        {
            if (GetHouseVariant(i + 1) == 3)
            {
                CreateUpperLadder(layout.transform, $"FireLadder_Upper_{labels[i]}_{labels[i + 1]}",
                    centers[i], centers[i + 1], houseRotations[i], trim, hydrantMetal);
            }
            else
            {
                CreateLadder(layout.transform, $"FireLadder_{labels[i]}_{labels[i + 1]}",
                    centers[i], centers[i + 1], trim, hydrantMetal);
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

    private static void ConfigureHouseVariant(GameObject house, int sectionNumber, Vector3 nextHouseCenter,
        Material wall, Material door, Material glass, Material roof, Material trim, Material concrete)
    {
        switch (GetHouseVariant(sectionNumber))
        {
            case 2:
                ConfigureElongatedHouse(house, wall, door);
                break;
            case 3:
                ConfigureTwoStoryHouse(house, nextHouseCenter, wall, door, glass, roof, trim, concrete);
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

        Transform frontDoor = house.transform.Find("Door");
        if (frontDoor != null)
            frontDoor.gameObject.SetActive(false);

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

        float frameZ = 4.60f * depthScale;
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

        Transform frontThreshold = house.transform.Find("Door_Threshold");
        if (frontThreshold != null)
            frontThreshold.gameObject.SetActive(false);
    }

    private static void ConfigureTwoStoryHouse(GameObject house, Vector3 nextHouseCenter,
        Material wall, Material door, Material glass, Material roof, Material trim, Material concrete)
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
        Vector3 balconyDirection = GetBalconyDirectionLocal(house.transform, nextHouseCenter);

        const float floorY = upperFloorBase - 0.12f;
        CreateUpperFloorWithStairOpening(house.transform, balconyDirection, floorY, concrete);

        CreateUpperStoryWalls(house.transform, balconyDirection, upperFloorCenter,
            upperFloorHeight, wall, door);
        CreateUpperWindows(house.transform, -balconyDirection, glass, trim);

        CreateHouseCube("Upper_Roof", house.transform,
            new Vector3(0f, upperFloorBase + upperFloorHeight + 0.22f, 0f), Vector3.zero,
            new Vector3(11.40f, 0.35f, 9.20f), roof);

        CreateBalcony(house.transform, balconyDirection, upperFloorBase + 0.10f, concrete, trim);

        CreateInteriorStaircase(house.transform, balconyDirection, concrete, trim);
    }

    private static Vector3 GetBalconyDirectionLocal(Transform house, Vector3 nextHouseCenter)
    {
        Vector3 worldDirection = nextHouseCenter - house.position;
        worldDirection.y = 0f;
        if (worldDirection.sqrMagnitude < 0.0001f)
            return Vector3.forward;

        Vector3 localDirection = Quaternion.Inverse(house.rotation) * worldDirection.normalized;
        if (Mathf.Abs(localDirection.x) > Mathf.Abs(localDirection.z))
            return localDirection.x >= 0f ? Vector3.right : Vector3.left;

        return localDirection.z >= 0f ? Vector3.forward : Vector3.back;
    }

    private static void CreateUpperStoryWalls(Transform house, Vector3 balconyDirection,
        float wallCenterY, float wallHeight, Material wall, Material door)
    {
        bool balconyOnPositiveZ = balconyDirection == Vector3.forward;
        bool balconyOnNegativeZ = balconyDirection == Vector3.back;
        bool balconyOnPositiveX = balconyDirection == Vector3.right;
        bool balconyOnNegativeX = balconyDirection == Vector3.left;

        if (!balconyOnNegativeZ)
            CreateHouseCube("Upper_Front_Wall", house,
                new Vector3(0f, wallCenterY, -4.25f), Vector3.zero,
                new Vector3(10.80f, wallHeight, 0.35f), wall);
        if (!balconyOnPositiveZ)
            CreateHouseCube("Upper_Back_Wall", house,
                new Vector3(0f, wallCenterY, 4.25f), Vector3.zero,
                new Vector3(10.80f, wallHeight, 0.35f), wall);
        if (!balconyOnNegativeX)
            CreateHouseCube("Upper_Left_Wall", house,
                new Vector3(-5.25f, wallCenterY, 0f), Vector3.zero,
                new Vector3(0.35f, wallHeight, 8.20f), wall);
        if (!balconyOnPositiveX)
            CreateHouseCube("Upper_Right_Wall", house,
                new Vector3(5.25f, wallCenterY, 0f), Vector3.zero,
                new Vector3(0.35f, wallHeight, 8.20f), wall);

        CreateUpperDoorWall(house, balconyDirection, wallCenterY, wallHeight, wall, door);
    }

    private static void CreateUpperDoorWall(Transform house, Vector3 direction,
        float wallCenterY, float wallHeight, Material wall, Material door)
    {
        const float wallTop = 10.32f;
        const float doorCenterY = 7.42f;
        const float segmentCenter = 3.22f;
        const float segmentSize = 4.37f;
        const float sideWall = 4.25f;
        const float frameFace = 4.60f;
        const float topHeight = wallTop - (doorCenterY + 1.60f);
        float topCenterY = wallTop - topHeight * 0.5f;

        if (direction == Vector3.forward || direction == Vector3.back)
        {
            float sign = direction.z;
            CreateHouseCube("Upper_BalconyWall_Left", house,
                new Vector3(-segmentCenter, wallCenterY, sign * sideWall), Vector3.zero,
                new Vector3(segmentSize, wallHeight, 0.35f), wall);
            CreateHouseCube("Upper_BalconyWall_Right", house,
                new Vector3(segmentCenter, wallCenterY, sign * sideWall), Vector3.zero,
                new Vector3(segmentSize, wallHeight, 0.35f), wall);
            CreateHouseCube("Upper_BalconyWall_Top", house,
                new Vector3(0f, topCenterY, sign * sideWall), Vector3.zero,
                new Vector3(2.06f, topHeight, 0.35f), wall);
            CreateHouseCube("Upper_Balcony_Frame_Left", house,
                new Vector3(-0.95f, doorCenterY, sign * frameFace), Vector3.zero,
                new Vector3(0.16f, 3.60f, 0.16f), door);
            CreateHouseCube("Upper_Balcony_Frame_Right", house,
                new Vector3(0.95f, doorCenterY, sign * frameFace), Vector3.zero,
                new Vector3(0.16f, 3.60f, 0.16f), door);
            CreateHouseCube("Upper_Balcony_Frame_Top", house,
                new Vector3(0f, 9.14f, sign * frameFace), Vector3.zero,
                new Vector3(2.05f, 0.16f, 0.16f), door);
            return;
        }

        float xSign = direction.x;
        CreateHouseCube("Upper_BalconyWall_Left", house,
            new Vector3(xSign * sideWall, wallCenterY, -segmentCenter), Vector3.zero,
            new Vector3(0.35f, wallHeight, segmentSize), wall);
        CreateHouseCube("Upper_BalconyWall_Right", house,
            new Vector3(xSign * sideWall, wallCenterY, segmentCenter), Vector3.zero,
            new Vector3(0.35f, wallHeight, segmentSize), wall);
        CreateHouseCube("Upper_BalconyWall_Top", house,
            new Vector3(xSign * sideWall, topCenterY, 0f), Vector3.zero,
            new Vector3(0.35f, topHeight, 2.06f), wall);
        CreateHouseCube("Upper_Balcony_Frame_Left", house,
            new Vector3(xSign * frameFace, doorCenterY, -0.95f), Vector3.zero,
            new Vector3(0.16f, 3.60f, 0.16f), door);
        CreateHouseCube("Upper_Balcony_Frame_Right", house,
            new Vector3(xSign * frameFace, doorCenterY, 0.95f), Vector3.zero,
            new Vector3(0.16f, 3.60f, 0.16f), door);
        CreateHouseCube("Upper_Balcony_Frame_Top", house,
            new Vector3(xSign * frameFace, 9.14f, 0f), Vector3.zero,
            new Vector3(0.16f, 0.16f, 2.05f), door);
    }

    private static void CreateUpperWindows(Transform house, Vector3 direction,
        Material glass, Material trim)
    {
        if (direction == Vector3.forward || direction == Vector3.back)
        {
            float sign = direction.z;
            CreateHouseCube("Upper_Window_Left", house,
                new Vector3(-3.05f, 8.30f, sign * 4.48f), Vector3.zero,
                new Vector3(1.75f, 1.45f, 0.14f), glass);
            CreateHouseCube("Upper_Window_Right", house,
                new Vector3(3.05f, 8.30f, sign * 4.48f), Vector3.zero,
                new Vector3(1.75f, 1.45f, 0.14f), glass);
            CreateHouseCube("Upper_Window_Sill_Left", house,
                new Vector3(-3.05f, 7.39f, sign * 4.70f), Vector3.zero,
                new Vector3(2.15f, 0.14f, 0.28f), trim);
            CreateHouseCube("Upper_Window_Sill_Right", house,
                new Vector3(3.05f, 7.39f, sign * 4.70f), Vector3.zero,
                new Vector3(2.15f, 0.14f, 0.28f), trim);
            return;
        }

        float signX = direction.x;
        CreateHouseCube("Upper_Window_Left", house,
            new Vector3(signX * 5.48f, 8.30f, -3.05f), Vector3.zero,
            new Vector3(0.14f, 1.45f, 1.75f), glass);
        CreateHouseCube("Upper_Window_Right", house,
            new Vector3(signX * 5.48f, 8.30f, 3.05f), Vector3.zero,
            new Vector3(0.14f, 1.45f, 1.75f), glass);
        CreateHouseCube("Upper_Window_Sill_Left", house,
            new Vector3(signX * 5.70f, 7.39f, -3.05f), Vector3.zero,
            new Vector3(0.28f, 0.14f, 2.15f), trim);
        CreateHouseCube("Upper_Window_Sill_Right", house,
            new Vector3(signX * 5.70f, 7.39f, 3.05f), Vector3.zero,
            new Vector3(0.28f, 0.14f, 2.15f), trim);
    }

    private static void CreateBalcony(Transform house, Vector3 direction, float balconyY,
        Material concrete, Material trim)
    {
        Vector3 side = new Vector3(-direction.z, 0f, direction.x);
        Vector3 center = direction * 5.35f;
        Vector3 platformScale = new Vector3(
            Mathf.Abs(side.x) * 4.60f + Mathf.Abs(direction.x) * 1.90f,
            0.20f,
            Mathf.Abs(side.z) * 4.60f + Mathf.Abs(direction.z) * 1.90f);
        CreateHouseCube("SecondFloor_Balcony", house,
            new Vector3(center.x, balconyY, center.z), Vector3.zero, platformScale, concrete);

        Vector3 sideRailScale = new Vector3(
            Mathf.Abs(side.x) * 0.14f + Mathf.Abs(direction.x) * 1.90f,
            1.20f,
            Mathf.Abs(side.z) * 0.14f + Mathf.Abs(direction.z) * 1.90f);
        CreateHouseCube("SecondFloor_Balcony_Rail_Left", house,
            new Vector3(center.x - side.x * 2.15f, balconyY + 0.62f, center.z - side.z * 2.15f),
            Vector3.zero, sideRailScale, trim);
        CreateHouseCube("SecondFloor_Balcony_Rail_Right", house,
            new Vector3(center.x + side.x * 2.15f, balconyY + 0.62f, center.z + side.z * 2.15f),
            Vector3.zero, sideRailScale, trim);

        Vector3 outerRailScale = new Vector3(
            Mathf.Abs(side.x) * 4.60f + Mathf.Abs(direction.x) * 0.14f,
            1.20f,
            Mathf.Abs(side.z) * 4.60f + Mathf.Abs(direction.z) * 0.14f);
        Vector3 outer = center + direction * 0.95f;
        CreateHouseCube("SecondFloor_Balcony_Rail_Outer", house,
            new Vector3(outer.x, balconyY + 0.62f, outer.z), Vector3.zero,
            outerRailScale, trim);
    }

    private static void CreateUpperFloorWithStairOpening(Transform house, Vector3 balconyDirection,
        float floorY, Material floorMaterial)
    {
        const float floorMin = -5.20f;
        const float floorMax = 5.20f;
        const float holeHalfWidth = 1.10f;
        const float holeMinAlong = -1.30f;
        const float holeMaxAlong = 4.20f;

        const bool stairRunsAlongZ = true;
        float stairSide = balconyDirection.x > 0.5f ? -3.55f : 3.55f;
        if (Mathf.Abs(balconyDirection.z) > 0.5f)
            stairSide = -3.55f;

        float holeMinX = stairRunsAlongZ ? stairSide - holeHalfWidth : holeMinAlong;
        float holeMaxX = stairRunsAlongZ ? stairSide + holeHalfWidth : holeMaxAlong;
        float holeMinZ = stairRunsAlongZ ? holeMinAlong : stairSide - holeHalfWidth;
        float holeMaxZ = stairRunsAlongZ ? holeMaxAlong : stairSide + holeHalfWidth;

        CreateFloorPiece("Upper_Floor_Left", house, floorMin, holeMinX, floorMin, floorMax, floorY, floorMaterial);
        CreateFloorPiece("Upper_Floor_Right", house, holeMaxX, floorMax, floorMin, floorMax, floorY, floorMaterial);
        CreateFloorPiece("Upper_Floor_Front", house, holeMinX, holeMaxX, floorMin, holeMinZ, floorY, floorMaterial);
        CreateFloorPiece("Upper_Floor_Back", house, holeMinX, holeMaxX, holeMaxZ, floorMax, floorY, floorMaterial);
    }

    private static void CreateFloorPiece(string name, Transform house, float minX, float maxX,
        float minZ, float maxZ, float y, Material material)
    {
        float width = maxX - minX;
        float depth = maxZ - minZ;
        if (width <= 0.05f || depth <= 0.05f)
            return;

        CreateHouseCube(name, house,
            new Vector3((minX + maxX) * 0.5f, y, (minZ + maxZ) * 0.5f), Vector3.zero,
            new Vector3(width, 0.24f, depth), material);
    }

    private static void CreateInteriorStaircase(Transform house, Vector3 balconyDirection,
        Material stepMaterial, Material railMaterial)
    {
        const int stepCount = 16;
        const float firstStepZ = -1.00f;
        const float stepRun = 0.32f;
        const float stepRise = 0.31f;
        const float stepWidth = 2.00f;
        const float baseY = 0.78f;
        const bool stairRunsAlongZ = true;
        float stairSide = balconyDirection.x > 0.5f ? -3.55f : 3.55f;
        if (Mathf.Abs(balconyDirection.z) > 0.5f)
            stairSide = -3.55f;
        Vector3 stairAxis = Vector3.forward;
        Vector3 stairSideOffset = new Vector3(stairSide, 0f, 0f);

        for (int i = 0; i < stepCount; i++)
        {
            float stepHeight = stepRise * (i + 1);
            Vector3 along = stairAxis * (firstStepZ + stepRun * (i + 0.5f));
            Vector3 position = stairSideOffset + along;
            Vector3 scale = stairRunsAlongZ
                ? new Vector3(stepWidth, stepHeight, stepRun)
                : new Vector3(stepRun, stepHeight, stepWidth);
            CreateHouseCube($"Interior_Stair_{i:00}", house,
                new Vector3(position.x, baseY + stepHeight * 0.5f, position.z), Vector3.zero,
                scale, stepMaterial);
        }

        float totalRun = stepRun * stepCount;
        float totalRise = stepRise * stepCount;
        Vector3 railDirection = (stairAxis * totalRun + Vector3.up * totalRise).normalized;
        Vector3 railRotation = Quaternion.LookRotation(railDirection, Vector3.up).eulerAngles;
        Vector3 railCenter = stairSideOffset + stairAxis * (firstStepZ + totalRun * 0.5f);
        railCenter.y = baseY + totalRise * 0.5f + 0.85f;
        Vector3 railSide = stairRunsAlongZ ? Vector3.right : Vector3.forward;
        float railLength = Mathf.Sqrt(totalRun * totalRun + totalRise * totalRise);

        CreateHouseCube("Interior_Stair_Rail_Left", house,
            railCenter - railSide * (stepWidth * 0.5f - 0.10f), railRotation,
            new Vector3(0.12f, 0.12f, railLength), railMaterial);
        CreateHouseCube("Interior_Stair_Rail_Right", house,
            railCenter + railSide * (stepWidth * 0.5f - 0.10f), railRotation,
            new Vector3(0.12f, 0.12f, railLength), railMaterial);
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

    private static Vector3 GetFireSpawnLocalPosition(int index, int count, bool twoStoryHouse)
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
        int upperStart = Mathf.Max(1, Mathf.CeilToInt(count * 0.5f));
        float y = twoStoryHouse && index >= upperStart ? 7.10f : 1.35f;
        return new Vector3(x, y, z);
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
        Vector3 localDirection = Quaternion.Inverse(houseRotation) * horizontalDirection;
        Vector3 balconyDirection = Mathf.Abs(localDirection.x) > Mathf.Abs(localDirection.z)
            ? (localDirection.x >= 0f ? Vector3.right : Vector3.left)
            : (localDirection.z >= 0f ? Vector3.forward : Vector3.back);
        Vector3 start = firstCenter + houseRotation * (balconyDirection * 6.20f + Vector3.up * 5.98f);
        Vector3 end = secondCenter - horizontalDirection * 9.05f + Vector3.up * 0.04f;
        CreateLadderBetweenPoints(parent, name, start, end, railMaterial, stepMaterial, 2.3f);
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
