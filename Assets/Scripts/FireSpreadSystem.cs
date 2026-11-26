using System.Collections.Generic;
using UnityEngine;

public class FireSpreadSystem : MonoBehaviour
{
    private static readonly List<FireSpreadSystem> instances = new List<FireSpreadSystem>();

    [Header("House Fire Starts")]
    [SerializeField] private Transform[] fireSpawnPoints = new Transform[0];
    [SerializeField] private Transform finalHouse;
    [SerializeField] private bool spawnAllHouseFiresAtStart = true;
    [SerializeField] private Vector3 houseSpreadBounds = new Vector3(5.1f, 2.6f, 4.1f);
    [SerializeField] private LayerMask spawnObstacleMask = ~0;
    [SerializeField] private float spawnClearanceRadius = 0.35f;

    [Header("Fire Spread")]
    [SerializeField] private float spreadInterval = 0.5f;
    [SerializeField] private float spreadRadius = 2f;
    [SerializeField] private float minFireSeparation = 0.65f;
    [SerializeField] private int maxFires = 24;
    [SerializeField] private float extinguishRadius = 0.5f;
    [SerializeField] private float contactRadius = 0.45f;

    [Header("Fire Particles")]
    [SerializeField] private int particlesPerFire = 36;
    [SerializeField] private float particleSize = 0.65f;

    private FireNode[] firePool;
    private Material fireMaterial;
    private int poolCursor;
    private int activeFireCount;
    private bool initialFiresSpawned;
    private bool allHousesExtinguished;
    private Transform[] houseRoots = new Transform[0];

    private sealed class FireNode
    {
        public GameObject gameObject;
        public Transform transform;
        public ParticleSystem particles;
        public Transform houseAnchor;
        public float nextSpreadTime;
        public bool countsForFinalObjective;
        public bool active;
    }

    public int ActiveFireCount => activeFireCount;
    public int ConfiguredSpawnPointCount => fireSpawnPoints == null ? 0 : fireSpawnPoints.Length;
    public int HouseCount
    {
        get
        {
            EnsureHouseRootsResolved();
            return houseRoots.Length;
        }
    }

    public int ExtinguishedHouseCount
    {
        get
        {
            EnsureHouseRootsResolved();
            return CountExtinguishedHouses();
        }
    }

    public bool IsAllHousesExtinguished => allHousesExtinguished;
    public event System.Action AllHousesExtinguished;
    public int ExtinguishedFireCount { get; private set; }
    public int FinalHouseInitialFireCount => CountInitialFiresForHouse(finalHouse);
    public int ExtinguishedFinalHouseFireCount { get; private set; }
    public int ActiveFinalHouseFireCount => CountActiveForHouse(finalHouse);
    public bool FinalHouseIsClear => initialFiresSpawned && finalHouse != null && ActiveFinalHouseFireCount == 0;
    public bool AllFiresExtinguished => initialFiresSpawned && activeFireCount == 0;

    private void OnEnable()
    {
        if (!instances.Contains(this))
            instances.Add(this);
    }

    private void OnDisable()
    {
        instances.Remove(this);
    }

    private void Awake()
    {
        fireMaterial = CreateFireMaterial();
        CreateFirePool();
    }

    private void Start()
    {
        ResolveFinalHouse();
        ResolveHouseRoots();
        if (spawnAllHouseFiresAtStart)
            SpawnInitialHouseFires();
    }

    private void Update()
    {
        if (!initialFiresSpawned)
            SpawnInitialHouseFires();

        CheckAllHousesExtinguished();

        for (int i = 0; i < firePool.Length; i++)
        {
            FireNode fire = firePool[i];
            if (!fire.active || Time.time < fire.nextSpreadTime)
                continue;

            TrySpreadFrom(fire);
            fire.nextSpreadTime = Time.time + spreadInterval;
        }
    }

    public static void NotifyWaterAt(Vector3 position, float waterRadius)
    {
        for (int i = instances.Count - 1; i >= 0; i--)
        {
            if (instances[i] != null)
                instances[i].ExtinguishAt(position, waterRadius);
        }
    }

    public static bool IsCapsuleInFire(Vector3 capsuleBottom, Vector3 capsuleTop, float capsuleRadius)
    {
        for (int i = instances.Count - 1; i >= 0; i--)
        {
            if (instances[i] != null && instances[i].CapsuleTouchesFire(capsuleBottom, capsuleTop, capsuleRadius))
                return true;
        }

        return false;
    }

    private void CreateFirePool()
    {
        firePool = new FireNode[Mathf.Max(1, maxFires)];

        for (int i = 0; i < firePool.Length; i++)
        {
            var fireObject = new GameObject("PooledFireParticle");
            fireObject.SetActive(false);

            var particleSystem = fireObject.AddComponent<ParticleSystem>();
            ConfigureFireParticles(particleSystem);

            firePool[i] = new FireNode
            {
                gameObject = fireObject,
                transform = fireObject.transform,
                particles = particleSystem
            };
        }
    }

    private void ConfigureFireParticles(ParticleSystem particleSystem)
    {
        var main = particleSystem.main;
        main.loop = true;
        main.playOnAwake = false;
        main.simulationSpace = ParticleSystemSimulationSpace.Local;
        main.maxParticles = particlesPerFire;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.45f, 0.9f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.25f, 0.8f);
        main.startSize = new ParticleSystem.MinMaxCurve(particleSize * 0.55f, particleSize);
        main.startColor = Color.white;

        var emission = particleSystem.emission;
        emission.enabled = true;
        emission.rateOverTime = new ParticleSystem.MinMaxCurve(22f, 34f);

        var shape = particleSystem.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = 0.22f;

        var colorOverLifetime = particleSystem.colorOverLifetime;
        colorOverLifetime.enabled = true;
        colorOverLifetime.color = new ParticleSystem.MinMaxGradient(CreateFireGradient());

        var renderer = particleSystem.GetComponent<ParticleSystemRenderer>();
        renderer.renderMode = ParticleSystemRenderMode.Billboard;
        renderer.material = fireMaterial;
    }

    private Gradient CreateFireGradient()
    {
        var gradient = new Gradient();
        gradient.SetKeys(
            new[]
            {
                new GradientColorKey(new Color(1f, 1f, 0.65f), 0f),
                new GradientColorKey(new Color(1f, 0.45f, 0.03f), 0.42f),
                new GradientColorKey(new Color(0.75f, 0.05f, 0.01f), 1f)
            },
            new[]
            {
                new GradientAlphaKey(0f, 0f),
                new GradientAlphaKey(1f, 0.12f),
                new GradientAlphaKey(0.9f, 0.7f),
                new GradientAlphaKey(0f, 1f)
            });
        return gradient;
    }

    private void SpawnInitialHouseFires()
    {
        initialFiresSpawned = true;
        if (fireSpawnPoints == null)
            return;

        for (int i = 0; i < fireSpawnPoints.Length; i++)
        {
            Transform spawnPoint = fireSpawnPoints[i];
            if (spawnPoint != null)
            {
                Transform house = FindHouseRoot(spawnPoint);
                SpawnFire(spawnPoint.position, house, house == finalHouse);
            }
        }
    }

    private void ResolveFinalHouse()
    {
        if (finalHouse != null || fireSpawnPoints == null)
            return;

        for (int i = 0; i < fireSpawnPoints.Length; i++)
        {
            Transform house = FindHouseRoot(fireSpawnPoints[i]);
            if (house != null && house.name == "House_Island_9")
            {
                finalHouse = house;
                return;
            }
        }
    }

    private void ResolveHouseRoots()
    {
        if (fireSpawnPoints == null)
        {
            houseRoots = new Transform[0];
            return;
        }

        var houses = new List<Transform>();
        for (int i = 0; i < fireSpawnPoints.Length; i++)
        {
            Transform spawnPoint = fireSpawnPoints[i];
            if (spawnPoint == null)
                continue;

            Transform house = FindHouseRoot(spawnPoint);
            if (house != null && !houses.Contains(house))
                houses.Add(house);
        }

        houseRoots = houses.ToArray();
    }

    private void EnsureHouseRootsResolved()
    {
        if (houseRoots.Length == 0 && fireSpawnPoints != null && fireSpawnPoints.Length > 0)
            ResolveHouseRoots();
    }

    private static Transform FindHouseRoot(Transform point)
    {
        Transform current = point;
        while (current != null)
        {
            if (current.name == "House" || current.name.StartsWith("House_Island_", System.StringComparison.Ordinal))
                return current;
            current = current.parent;
        }

        return point;
    }

    private void TrySpreadFrom(FireNode source)
    {
        if (activeFireCount >= firePool.Length)
            return;

        for (int attempt = 0; attempt < 8; attempt++)
        {
            Vector3 point = source.transform.position + Random.insideUnitSphere * spreadRadius;
            if (source.houseAnchor != null)
            {
                Vector3 localOffset = point - source.houseAnchor.position;
                localOffset.x = Mathf.Clamp(localOffset.x, -houseSpreadBounds.x, houseSpreadBounds.x);
                localOffset.y = Mathf.Clamp(localOffset.y, -houseSpreadBounds.y, houseSpreadBounds.y);
                localOffset.z = Mathf.Clamp(localOffset.z, -houseSpreadBounds.z, houseSpreadBounds.z);
                point = source.houseAnchor.position + localOffset;
            }

            if (IsSpawnPointClear(point)
                && !IsFireNear(point, minFireSeparation)
                && SpawnFire(point, source.houseAnchor, false))
                return;
        }
    }

    private bool SpawnFire(Vector3 position, Transform houseAnchor, bool countsForFinalObjective)
    {
        FireNode fire = GetAvailableFire();
        if (fire == null)
            return false;

        fire.transform.position = position;
        fire.houseAnchor = houseAnchor;
        fire.nextSpreadTime = Time.time + spreadInterval;
        fire.countsForFinalObjective = countsForFinalObjective;
        fire.active = true;
        fire.gameObject.SetActive(true);
        fire.particles.Clear();
        fire.particles.Play();
        activeFireCount++;
        return true;
    }

    private FireNode GetAvailableFire()
    {
        for (int i = 0; i < firePool.Length; i++)
        {
            int index = (poolCursor + i) % firePool.Length;
            if (!firePool[index].active)
            {
                poolCursor = (index + 1) % firePool.Length;
                return firePool[index];
            }
        }

        return null;
    }

    private void ExtinguishAt(Vector3 position, float waterRadius)
    {
        float radius = extinguishRadius + waterRadius;
        float radiusSqr = radius * radius;

        for (int i = 0; i < firePool.Length; i++)
        {
            FireNode fire = firePool[i];
            if (fire.active && (fire.transform.position - position).sqrMagnitude <= radiusSqr)
                ReleaseFire(fire);
        }

        CheckAllHousesExtinguished();
    }

    private void CheckAllHousesExtinguished()
    {
        if (allHousesExtinguished || !initialFiresSpawned)
            return;

        int totalHouses = HouseCount;
        if (totalHouses <= 0 || ExtinguishedHouseCount < totalHouses)
            return;

        allHousesExtinguished = true;
        AllHousesExtinguished?.Invoke();
    }

    private bool CapsuleTouchesFire(Vector3 capsuleBottom, Vector3 capsuleTop, float capsuleRadius)
    {
        Vector3 segment = capsuleTop - capsuleBottom;
        float segmentLengthSqr = segment.sqrMagnitude;
        float contactDistance = capsuleRadius + contactRadius;
        float contactDistanceSqr = contactDistance * contactDistance;

        for (int i = 0; i < firePool.Length; i++)
        {
            FireNode fire = firePool[i];
            if (!fire.active)
                continue;

            float t = segmentLengthSqr > 0.0001f
                ? Mathf.Clamp01(Vector3.Dot(fire.transform.position - capsuleBottom, segment) / segmentLengthSqr)
                : 0f;
            Vector3 closestPoint = capsuleBottom + segment * t;
            if ((fire.transform.position - closestPoint).sqrMagnitude <= contactDistanceSqr)
                return true;
        }

        return false;
    }

    private void ReleaseFire(FireNode fire)
    {
        fire.active = false;
        fire.houseAnchor = null;
        if (fire.countsForFinalObjective)
            ExtinguishedFinalHouseFireCount++;
        fire.countsForFinalObjective = false;
        fire.particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        fire.gameObject.SetActive(false);
        activeFireCount = Mathf.Max(0, activeFireCount - 1);
        ExtinguishedFireCount++;
    }

    private int CountActiveForHouse(Transform house)
    {
        if (house == null || firePool == null)
            return 0;

        int count = 0;
        for (int i = 0; i < firePool.Length; i++)
        {
            FireNode fire = firePool[i];
            if (fire.active && fire.houseAnchor == house)
                count++;
        }

        return count;
    }

    private int CountInitialFiresForHouse(Transform house)
    {
        if (house == null || fireSpawnPoints == null)
            return 0;

        int count = 0;
        for (int i = 0; i < fireSpawnPoints.Length; i++)
        {
            if (FindHouseRoot(fireSpawnPoints[i]) == house)
                count++;
        }

        return count;
    }

    private int CountExtinguishedHouses()
    {
        if (!initialFiresSpawned)
            return 0;

        int count = 0;
        for (int i = 0; i < houseRoots.Length; i++)
        {
            Transform house = houseRoots[i];
            if (house != null && CountActiveForHouse(house) == 0)
                count++;
        }

        return count;
    }

    private bool IsFireNear(Vector3 point, float distance)
    {
        float distanceSqr = distance * distance;
        for (int i = 0; i < firePool.Length; i++)
        {
            FireNode fire = firePool[i];
            if (fire.active && (fire.transform.position - point).sqrMagnitude < distanceSqr)
                return true;
        }

        return false;
    }

    private bool IsSpawnPointClear(Vector3 point)
    {
        return !Physics.CheckSphere(
            point,
            spawnClearanceRadius,
            spawnObstacleMask,
            QueryTriggerInteraction.Ignore);
    }

    private Material CreateFireMaterial()
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
        if (shader == null)
            shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null)
            shader = Shader.Find("Particles/Standard Unlit");

        if (shader == null)
            return null;

        var material = new Material(shader)
        {
            name = "Runtime Fire Particle Material",
            color = Color.white
        };

        if (material.HasProperty("_BaseColor"))
            material.SetColor("_BaseColor", Color.white);
        if (material.HasProperty("_Color"))
            material.SetColor("_Color", Color.white);
        if (material.HasProperty("_BaseMap"))
            material.SetTexture("_BaseMap", Texture2D.whiteTexture);
        if (material.HasProperty("_MainTex"))
            material.SetTexture("_MainTex", Texture2D.whiteTexture);

        return material;
    }

    private void OnDestroy()
    {
        if (fireMaterial != null)
            Destroy(fireMaterial);
    }
}
