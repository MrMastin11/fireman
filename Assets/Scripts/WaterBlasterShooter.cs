using System.Collections;
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
    [SerializeField] private int dropletsPerShot = 18;
    [SerializeField] private float spread = 0.018f;
    [SerializeField] private float dropletSize = 0.2f;
    [SerializeField] private float streamWidth = 0.24f;
    [SerializeField] private float streamLifetime = 1.2f;
    [SerializeField] private LayerMask hitMask = ~0;

    [Header("World Muzzle")]
    [SerializeField] private Vector2 muzzleViewportPosition = new Vector2(0.78f, 0.19f);
    [SerializeField] private float muzzleDepth = 0.45f;

    private float nextShotTime;
    private Material waterMaterial;

    private void Awake()
    {
        if (playerCamera == null)
            playerCamera = GetComponentInChildren<Camera>();

        if (playerController == null)
            playerController = GetComponent<FirstPersonController>();

        waterMaterial = CreateWaterMaterial();
    }

    private void Update()
    {
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
        if (playerCamera == null || waterMaterial == null)
            return;

        Ray aimRay = playerCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
        Vector3 target = aimRay.origin + aimRay.direction * maxDistance;

        if (Physics.Raycast(aimRay, out RaycastHit hit, maxDistance, hitMask, QueryTriggerInteraction.Ignore)
            && !hit.collider.transform.IsChildOf(transform))
        {
            target = hit.point;
        }

        Vector3 muzzle = playerCamera.ViewportToWorldPoint(
            new Vector3(muzzleViewportPosition.x, muzzleViewportPosition.y, muzzleDepth));
        Vector3 direction = (target - muzzle).normalized;
        float distance = Vector3.Distance(muzzle, target);
        float lifetime = Mathf.Clamp(distance / particleSpeed, 2.5f, 5f);

        CreateWorldWaterStream(muzzle, target);

        for (int i = 0; i < dropletsPerShot; i++)
        {
            Vector3 particleDirection = (direction + Random.insideUnitSphere * spread).normalized;
            Vector3 start = muzzle + Random.insideUnitSphere * 0.035f;
            float dropletLifetime = lifetime * Random.Range(0.88f, 1.08f);
            float size = dropletSize * Random.Range(0.75f, 1.25f);
            StartCoroutine(MoveWorldDroplet(start, particleDirection, dropletLifetime, size));
        }

        if (playerController != null)
            playerController.ApplyShotRecoil();
    }

    private void CreateWorldWaterStream(Vector3 start, Vector3 target)
    {
        var streamObject = new GameObject("WorldWaterStream");
        Vector3 direction = (target - start).normalized;
        float distance = Vector3.Distance(start, target);
        int streamDrops = Mathf.Clamp(Mathf.CeilToInt(distance / 0.55f), 8, 28);

        for (int i = 0; i < streamDrops; i++)
        {
            float t = (i + 1f) / (streamDrops + 1f);
            Vector3 position = Vector3.Lerp(start, target, t);
            position += Random.insideUnitSphere * streamWidth * 0.28f;

            var drop = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            drop.name = "WorldWaterStreamDrop";
            drop.transform.SetParent(streamObject.transform);
            drop.transform.position = position;
            float diameter = streamWidth * Random.Range(0.72f, 1.25f);
            drop.transform.localScale = Vector3.one * diameter;

            var collider = drop.GetComponent<Collider>();
            if (collider != null)
                collider.enabled = false;

            var renderer = drop.GetComponent<Renderer>();
            renderer.sharedMaterial = waterMaterial;
        }

        Destroy(streamObject, streamLifetime);
    }

    private IEnumerator MoveWorldDroplet(Vector3 start, Vector3 direction, float lifetime, float size)
    {
        var droplet = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        droplet.name = "WorldWaterDroplet";
        droplet.transform.position = start;
        droplet.transform.localScale = Vector3.one * size;

        var collider = droplet.GetComponent<Collider>();
        if (collider != null)
            collider.enabled = false;

        var renderer = droplet.GetComponent<Renderer>();
        renderer.sharedMaterial = waterMaterial;

        float elapsed = 0f;
        while (elapsed < lifetime && droplet != null)
        {
            elapsed += Time.deltaTime;
            droplet.transform.position += direction * particleSpeed * Time.deltaTime;
            yield return null;
        }

        if (droplet != null)
            Destroy(droplet);
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
}
