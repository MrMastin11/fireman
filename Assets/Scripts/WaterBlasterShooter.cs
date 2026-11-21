using UnityEngine;
using UnityEngine.InputSystem;

public class WaterBlasterShooter : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Camera playerCamera;
    [SerializeField] private FirstPersonController playerController;

    [Header("Shooting")]
    [SerializeField] private float shotsPerSecond = 8f;
    [SerializeField] private float particleSpeed = 34f;
    [SerializeField] private float maxDistance = 80f;
    [SerializeField] private int dropletsPerShot = 24;
    [SerializeField] private float spread = 0.018f;
    [SerializeField] private float dropletSize = 0.2f;
    [SerializeField] private LayerMask hitMask = ~0;

    [Header("Droplet Physics")]
    [SerializeField] private int poolSize = 256;
    [SerializeField] private float projectileMaxDistance = 35f;
    [SerializeField] private float maxDropletLifetime = 1.5f;
    [SerializeField] private float gravity = -18f;
    [SerializeField] private float bounceDamping = 0.55f;
    [SerializeField] private int maxBounces = 2;
    [SerializeField] private float collisionRadius = 0.08f;

    [Header("World Muzzle")]
    [SerializeField] private Vector2 muzzleViewportPosition = new Vector2(0.62f, 0.34f);
    [SerializeField] private float muzzleDepth = 0.45f;
    [SerializeField] private float muzzleForwardOffset = 0.1f;

    private float nextShotTime;
    private Material waterMaterial;
    private WaterDroplet[] dropletPool;
    private int poolCursor;
    private readonly RaycastHit[] hitBuffer = new RaycastHit[8];

    private sealed class WaterDroplet
    {
        public GameObject gameObject;
        public Transform transform;
        public Renderer renderer;
        public Vector3 velocity;
        public float distanceTravelled;
        public float remainingLifetime;
        public float radius;
        public int bounces;
        public bool active;
    }

    private void Awake()
    {
        if (playerCamera == null)
            playerCamera = GetComponentInChildren<Camera>();

        if (playerController == null)
            playerController = GetComponent<FirstPersonController>();

        waterMaterial = CreateWaterMaterial();
        CreateDropletPool();
    }

    private void Update()
    {
        SimulateDroplets(Time.deltaTime);

        if (Mouse.current == null)
            return;

        if (Mouse.current.leftButton.isPressed && Time.time >= nextShotTime)
        {
            ShootOnce();
            nextShotTime = Time.time + 1f / Mathf.Max(1f, shotsPerSecond);
        }
    }

    public void ShootOnce()
    {
        if (playerCamera == null || waterMaterial == null || dropletPool == null)
            return;

        Ray aimRay = playerCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
        Vector3 target = aimRay.origin + aimRay.direction * maxDistance;

        if (Physics.Raycast(aimRay, out RaycastHit hit, maxDistance, hitMask, QueryTriggerInteraction.Ignore)
            && !IsOwnedCollider(hit.collider))
        {
            target = hit.point;
        }

        Vector3 muzzle = playerCamera.ViewportToWorldPoint(
            new Vector3(muzzleViewportPosition.x, muzzleViewportPosition.y, muzzleDepth));
        Vector3 direction = (target - muzzle).normalized;

        for (int i = 0; i < dropletsPerShot; i++)
        {
            Vector3 particleDirection = (direction + Random.insideUnitSphere * spread).normalized;
            Vector3 start = muzzle + direction * muzzleForwardOffset + Random.insideUnitSphere * 0.035f;
            float size = dropletSize * Random.Range(0.75f, 1.25f);
            SpawnDroplet(start, particleDirection * particleSpeed, size);
        }

        if (playerController != null)
            playerController.ApplyShotRecoil();
    }

    private void CreateDropletPool()
    {
        int safePoolSize = Mathf.Max(poolSize, dropletsPerShot);
        dropletPool = new WaterDroplet[safePoolSize];

        for (int i = 0; i < dropletPool.Length; i++)
        {
            var dropletObject = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            dropletObject.name = "PooledWaterDroplet";
            dropletObject.SetActive(false);

            var collider = dropletObject.GetComponent<Collider>();
            if (collider != null)
                collider.enabled = false;

            var renderer = dropletObject.GetComponent<Renderer>();
            renderer.sharedMaterial = waterMaterial;

            dropletPool[i] = new WaterDroplet
            {
                gameObject = dropletObject,
                transform = dropletObject.transform,
                renderer = renderer
            };
        }
    }

    private void SpawnDroplet(Vector3 position, Vector3 velocity, float diameter)
    {
        WaterDroplet droplet = GetNextDroplet();
        droplet.transform.SetPositionAndRotation(position, Random.rotation);
        droplet.transform.localScale = Vector3.one * diameter;
        droplet.velocity = velocity;
        droplet.distanceTravelled = 0f;
        droplet.remainingLifetime = maxDropletLifetime;
        droplet.radius = Mathf.Max(collisionRadius, diameter * 0.5f);
        droplet.bounces = 0;
        droplet.active = true;
        droplet.renderer.enabled = true;
        droplet.gameObject.SetActive(true);
    }

    private WaterDroplet GetNextDroplet()
    {
        for (int i = 0; i < dropletPool.Length; i++)
        {
            int index = (poolCursor + i) % dropletPool.Length;
            if (!dropletPool[index].active)
            {
                poolCursor = (index + 1) % dropletPool.Length;
                return dropletPool[index];
            }
        }

        WaterDroplet recycled = dropletPool[poolCursor];
        poolCursor = (poolCursor + 1) % dropletPool.Length;
        ReleaseDroplet(recycled);
        return recycled;
    }

    private void SimulateDroplets(float deltaTime)
    {
        if (dropletPool == null || deltaTime <= 0f)
            return;

        for (int i = 0; i < dropletPool.Length; i++)
        {
            WaterDroplet droplet = dropletPool[i];
            if (!droplet.active)
                continue;

            droplet.remainingLifetime -= deltaTime;
            if (droplet.remainingLifetime <= 0f || droplet.distanceTravelled >= projectileMaxDistance)
            {
                ReleaseDroplet(droplet);
                continue;
            }

            droplet.velocity += Vector3.up * gravity * deltaTime;
            Vector3 displacement = droplet.velocity * deltaTime;
            float travelDistance = displacement.magnitude;

            if (travelDistance > 0.0001f
                && TryGetClosestHit(droplet.transform.position, displacement.normalized, travelDistance,
                    droplet.radius, out RaycastHit hit))
            {
                droplet.transform.position = hit.point + hit.normal * (droplet.radius + 0.005f);
                droplet.distanceTravelled += hit.distance;
                droplet.velocity = Vector3.Reflect(droplet.velocity, hit.normal) * bounceDamping;
                droplet.bounces++;

                if (droplet.bounces > maxBounces || droplet.velocity.sqrMagnitude < 4f)
                    ReleaseDroplet(droplet);
            }
            else
            {
                droplet.transform.position += displacement;
                droplet.distanceTravelled += travelDistance;
            }
        }
    }

    private bool TryGetClosestHit(Vector3 origin, Vector3 direction, float distance, float radius,
        out RaycastHit closestHit)
    {
        int hitCount = Physics.SphereCastNonAlloc(
            origin,
            radius,
            direction,
            hitBuffer,
            distance,
            hitMask,
            QueryTriggerInteraction.Ignore);

        float closestDistance = float.MaxValue;
        closestHit = default;
        bool foundHit = false;

        for (int i = 0; i < hitCount; i++)
        {
            RaycastHit hit = hitBuffer[i];
            if (hit.collider == null || IsOwnedCollider(hit.collider) || hit.distance >= closestDistance)
                continue;

            closestDistance = hit.distance;
            closestHit = hit;
            foundHit = true;
        }

        return foundHit;
    }

    private bool IsOwnedCollider(Collider collider)
    {
        return collider != null
            && (collider.transform == transform || collider.transform.IsChildOf(transform));
    }

    private void ReleaseDroplet(WaterDroplet droplet)
    {
        droplet.active = false;
        droplet.velocity = Vector3.zero;
        droplet.renderer.enabled = false;
        droplet.gameObject.SetActive(false);
    }

    private Material CreateWaterMaterial()
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null)
            shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
        if (shader == null)
            shader = Shader.Find("Standard");

        if (shader == null)
            return null;

        var waterColor = new Color(0.2f, 0.8f, 1f, 0.95f);
        var material = new Material(shader)
        {
            name = "World Water Droplet Material",
            color = waterColor
        };

        if (material.HasProperty("_BaseColor"))
            material.SetColor("_BaseColor", waterColor);
        if (material.HasProperty("_Color"))
            material.SetColor("_Color", waterColor);

        return material;
    }

    private void OnDestroy()
    {
        if (dropletPool != null)
        {
            for (int i = 0; i < dropletPool.Length; i++)
            {
                if (dropletPool[i] != null && dropletPool[i].gameObject != null)
                    Destroy(dropletPool[i].gameObject);
            }
        }

        if (waterMaterial != null)
            Destroy(waterMaterial);
    }
}
