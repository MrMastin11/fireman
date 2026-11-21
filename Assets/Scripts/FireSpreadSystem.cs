using System.Collections.Generic;
using UnityEngine;

public class FireSpreadSystem : MonoBehaviour
{
    private static readonly List<FireSpreadSystem> instances = new List<FireSpreadSystem>();

    [Header("Spawn Area")]
    [SerializeField] private Vector3 spawnAreaCenter = new Vector3(0f, 1.5f, -4.35f);
    [SerializeField] private Vector3 spawnAreaSize = new Vector3(6.5f, 3.2f, 2.2f);
    [SerializeField] private bool firstSpawnImmediately = true;
    [SerializeField] private float spawnInterval = 10f;

    [Header("Fire Spread")]
    [SerializeField] private float spreadInterval = 2f;
    [SerializeField] private float spreadRadius = 2f;
    [SerializeField] private float minFireSeparation = 0.65f;
    [SerializeField] private int maxFires = 24;
    [SerializeField] private float extinguishRadius = 0.5f;

    [Header("Fire Particles")]
    [SerializeField] private int particlesPerFire = 36;
    [SerializeField] private float particleSize = 0.65f;

    private FireNode[] firePool;
    private Material fireMaterial;
    private float nextSpawnTime;
    private int poolCursor;
    private int activeFireCount;

    private sealed class FireNode
    {
        public GameObject gameObject;
        public Transform transform;
        public ParticleSystem particles;
        public float nextSpreadTime;
        public bool active;
    }

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
        nextSpawnTime = firstSpawnImmediately ? Time.time : Time.time + spawnInterval;
    }

    private void Update()
    {
        if (Time.time >= nextSpawnTime)
        {
            TrySpawnRandomFire();
            nextSpawnTime = Time.time + spawnInterval;
        }

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

    private void TrySpawnRandomFire()
    {
        for (int attempt = 0; attempt < 12; attempt++)
        {
            Vector3 point = GetRandomPointInSpawnArea();
            if (!IsFireNear(point, minFireSeparation) && SpawnFire(point))
                return;
        }
    }

    private void TrySpreadFrom(FireNode source)
    {
        if (activeFireCount >= firePool.Length)
            return;

        for (int attempt = 0; attempt < 8; attempt++)
        {
            Vector3 point = source.transform.position + Random.insideUnitSphere * spreadRadius;
            point = ClampToSpawnArea(point);

            if (!IsFireNear(point, minFireSeparation) && SpawnFire(point))
                return;
        }
    }

    private bool SpawnFire(Vector3 position)
    {
        FireNode fire = GetAvailableFire();
        if (fire == null)
            return false;

        fire.transform.position = position;
        fire.nextSpreadTime = Time.time + spreadInterval;
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
    }

    private void ReleaseFire(FireNode fire)
    {
        fire.active = false;
        fire.particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        fire.gameObject.SetActive(false);
        activeFireCount = Mathf.Max(0, activeFireCount - 1);
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

    private Vector3 GetRandomPointInSpawnArea()
    {
        Vector3 halfSize = spawnAreaSize * 0.5f;
        return spawnAreaCenter + new Vector3(
            Random.Range(-halfSize.x, halfSize.x),
            Random.Range(-halfSize.y, halfSize.y),
            Random.Range(-halfSize.z, halfSize.z));
    }

    private Vector3 ClampToSpawnArea(Vector3 point)
    {
        Vector3 halfSize = spawnAreaSize * 0.5f;
        point.x = Mathf.Clamp(point.x, spawnAreaCenter.x - halfSize.x, spawnAreaCenter.x + halfSize.x);
        point.y = Mathf.Clamp(point.y, spawnAreaCenter.y - halfSize.y, spawnAreaCenter.y + halfSize.y);
        point.z = Mathf.Clamp(point.z, spawnAreaCenter.z - halfSize.z, spawnAreaCenter.z + halfSize.z);
        return point;
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
