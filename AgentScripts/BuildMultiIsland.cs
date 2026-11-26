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

        oldGround.SetActive(false);
        path.transform.position = new Vector3(0f, -0.04f, -7.35f);
        path.transform.localScale = new Vector3(3.4f, 0.12f, 4.2f);

        if (playerStart != null)
            playerStart.position = new Vector3(0f, 1f, -8.2f);
        if (entranceTarget != null)
            entranceTarget.position = new Vector3(0f, 1f, -4.8f);

        MoveExistingTrees(environment.transform);

        Material grass = LoadMaterial("Assets/Materials/Grass.mat");
        Material concrete = LoadMaterial("Assets/Materials/Concrete.mat");
        Material trim = LoadMaterial("Assets/Materials/House_Trim.mat");
        Material hydrantMetal = LoadMaterial("Assets/Materials/Hydrant_Metal.mat");
        Material water = GetOrCreateWaterMaterial();

        GameObject layout = new GameObject(LayoutName);
        layout.transform.SetParent(environment.transform, false);
        layout.isStatic = true;

        CreateCube("Water_Between_Islands", layout.transform, new Vector3(0f, -0.95f, 0f),
            Vector3.zero, new Vector3(84f, 0.5f, 84f), water);

        const float islandSpacing = 24f;
        const float levelRise = 1.5f;
        Vector3[] centers =
        {
            new Vector3(0f, 0f, 0f),
            new Vector3(0f, levelRise * 1f, islandSpacing),
            new Vector3(islandSpacing, levelRise * 2f, islandSpacing),
            new Vector3(islandSpacing, levelRise * 3f, 0f),
            new Vector3(islandSpacing, levelRise * 4f, -islandSpacing),
            new Vector3(0f, levelRise * 5f, -islandSpacing),
            new Vector3(-islandSpacing, levelRise * 6f, -islandSpacing),
            new Vector3(-islandSpacing, levelRise * 7f, 0f),
            new Vector3(-islandSpacing, levelRise * 8f, islandSpacing)
        };
        string[] labels = { "1", "2", "3", "4", "5", "6", "7", "8", "9" };
        var fireSpawnPoints = new List<Transform>(centers.Length);

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

            if (i == 0)
            {
                house.transform.SetParent(island.transform, true);
            }
            else
            {
                GameObject clone = UnityEngine.Object.Instantiate(house, island.transform);
                clone.name = $"House_Island_{labels[i]}";
                clone.transform.position = centers[i];
                clone.transform.rotation = Quaternion.identity;
                clone.transform.localScale = Vector3.one;

                GameObject clonedPath = UnityEngine.Object.Instantiate(path, island.transform);
                clonedPath.name = $"Front_Path_Island_{labels[i]}";
                clonedPath.transform.position = centers[i] + new Vector3(0f, -0.04f, -7.35f);
                clonedPath.transform.rotation = Quaternion.identity;
                clonedPath.transform.localScale = new Vector3(3.4f, 0.12f, 4.2f);

                GameObject clonedHydrant = UnityEngine.Object.Instantiate(hydrant, island.transform);
                clonedHydrant.name = $"Fire_Hydrant_Island_{labels[i]}";
                clonedHydrant.transform.position = centers[i] + new Vector3(-7f, 0f, -6f);
                clonedHydrant.transform.rotation = Quaternion.identity;
                clonedHydrant.transform.localScale = Vector3.one;

                GameObject clonedTree = UnityEngine.Object.Instantiate(firstTree, island.transform);
                clonedTree.name = $"Tree_Island_{labels[i]}";
                clonedTree.transform.position = centers[i] + new Vector3(5.5f, 0f, 4.2f);
                clonedTree.transform.rotation = Quaternion.identity;
                clonedTree.transform.localScale = Vector3.one;
            }

            GameObject spawn = new GameObject($"FireSpawnPoint_{labels[i]}");
            spawn.transform.SetParent(island.transform, false);
            spawn.transform.localPosition = new Vector3(0f, 1.35f, 0f);
            spawn.transform.localRotation = Quaternion.identity;
            fireSpawnPoints.Add(spawn.transform);
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

        fireSerialized.FindProperty("spawnAllHouseFiresAtStart").boolValue = true;
        fireSerialized.FindProperty("houseSpreadBounds").vector3Value = new Vector3(5.1f, 2.6f, 4.1f);
        fireSerialized.FindProperty("spreadInterval").floatValue = 0.5f;
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

    private static void MoveExistingTrees(Transform environment)
    {
        var trees = environment.GetComponentsInChildren<Transform>(true)
            .Where(t => t.name == "Tree")
            .ToArray();
        if (trees.Length > 0)
            trees[0].position = new Vector3(-5.5f, 0f, 4.2f);
        if (trees.Length > 1)
            trees[1].position = new Vector3(5.5f, 0f, 4.2f);
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
