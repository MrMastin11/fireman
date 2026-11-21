using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class WaterBlasterShooter : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Camera playerCamera;
    [SerializeField] private RectTransform weaponOverlay;
    [SerializeField] private FirstPersonController playerController;
    [SerializeField] private ParticleSystem waterParticles;

    [Header("Shooting")]
    [SerializeField] private float shotsPerSecond = 8f;
    [SerializeField] private float particleSpeed = 18f;
    [SerializeField] private float maxDistance = 80f;
    [SerializeField] private int dropletsPerShot = 10;
    [SerializeField] private float spread = 0.025f;
    [SerializeField] private LayerMask hitMask = ~0;

    [Header("Screen Positions")]
    [SerializeField] private Vector2 muzzleViewportPosition = new Vector2(0.78f, 0.19f);
    [SerializeField] private float muzzleDepth = 0.45f;

    private float nextShotTime;
    private RectTransform projectileContainer;
    private Sprite dropletSprite;

    private void Awake()
    {
        if (playerCamera == null)
            playerCamera = GetComponentInChildren<Camera>();

        if (playerController == null)
            playerController = GetComponent<FirstPersonController>();

        if (waterParticles == null)
            waterParticles = CreateWaterParticleSystem();

        CreateProjectileContainer();
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
        if (playerCamera == null || waterParticles == null)
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
        float lifetime = Mathf.Clamp(distance / particleSpeed, 2f, 4f);

        for (int i = 0; i < dropletsPerShot; i++)
        {
            Vector3 particleDirection = (direction + Random.insideUnitSphere * spread).normalized;
            var emitParams = new ParticleSystem.EmitParams
            {
                position = muzzle,
                velocity = particleDirection * particleSpeed,
                startLifetime = lifetime * Random.Range(0.88f, 1.08f),
                startSize = Random.Range(0.07f, 0.11f),
                startColor = new Color(0.35f, 0.85f, 1f, 0.95f)
            };
            waterParticles.Emit(emitParams, 1);

            StartCoroutine(AnimateScreenDroplet(
                i * 0.012f,
                Random.Range(0.8f, 1f),
                Random.Range(6f, 10f)));
        }

        if (playerController != null)
            playerController.ApplyShotRecoil();
    }

    private void CreateProjectileContainer()
    {
        if (weaponOverlay == null)
            return;

        var canvasTransform = weaponOverlay.transform.parent;
        if (canvasTransform == null)
            return;

        projectileContainer = canvasTransform.Find("WaterProjectiles") as RectTransform;
        if (projectileContainer == null)
        {
            var containerObject = new GameObject("WaterProjectiles", typeof(RectTransform));
            containerObject.transform.SetParent(canvasTransform, false);
            projectileContainer = containerObject.GetComponent<RectTransform>();
            projectileContainer.SetSiblingIndex(1);
        }

        projectileContainer.anchorMin = new Vector2(0.5f, 0.5f);
        projectileContainer.anchorMax = new Vector2(0.5f, 0.5f);
        projectileContainer.pivot = new Vector2(0.5f, 0.5f);
        projectileContainer.anchoredPosition = Vector2.zero;
        projectileContainer.sizeDelta = new Vector2(1920f, 1080f);
    }

    private IEnumerator AnimateScreenDroplet(float delay, float duration, float size)
    {
        if (projectileContainer == null)
            yield break;

        if (delay > 0f)
            yield return new WaitForSecondsRealtime(delay);

        var dropletObject = new GameObject("WaterDroplet", typeof(RectTransform), typeof(Image));
        dropletObject.transform.SetParent(projectileContainer, false);
        dropletObject.transform.SetAsLastSibling();

        var rect = dropletObject.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = new Vector2(size * 1.8f, size);

        var image = dropletObject.GetComponent<Image>();
        image.sprite = GetDropletSprite();
        image.color = new Color(0.35f, 0.9f, 1f, 0.95f);
        image.raycastTarget = false;

        Vector2 start = new Vector2(0.78f * 1920f - 960f, 0.19f * 1080f - 540f);
        Vector2 target = Vector2.zero;
        float elapsed = 0f;
        Vector2 spreadOffset = Random.insideUnitCircle * 12f;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float progress = Mathf.Clamp01(elapsed / duration);
            float easedProgress = Mathf.SmoothStep(0f, 1f, progress);
            rect.anchoredPosition = Vector2.Lerp(start + spreadOffset, target + spreadOffset * 0.15f, easedProgress);
            rect.localRotation = Quaternion.Euler(0f, 0f, Mathf.Lerp(-12f, 8f, easedProgress));
            yield return null;
        }

        Destroy(dropletObject);
    }

    private Sprite GetDropletSprite()
    {
        if (dropletSprite == null)
        {
            dropletSprite = Sprite.Create(
                Texture2D.whiteTexture,
                new Rect(0f, 0f, 1f, 1f),
                new Vector2(0.5f, 0.5f),
                1f);
        }

        return dropletSprite;
    }

    private ParticleSystem CreateWaterParticleSystem()
    {
        var particleObject = new GameObject("WaterShotParticles");
        var particles = particleObject.AddComponent<ParticleSystem>();
        var main = particles.main;
        main.loop = false;
        main.playOnAwake = false;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.startLifetime = 1.25f;
        main.startSpeed = 0f;
        main.startSize = 0.09f;
        main.startColor = new Color(0.35f, 0.85f, 1f, 0.95f);
        main.maxParticles = 512;

        var emission = particles.emission;
        emission.enabled = false;

        var shape = particles.shape;
        shape.enabled = false;

        var renderer = particleObject.GetComponent<ParticleSystemRenderer>();
        renderer.renderMode = ParticleSystemRenderMode.Billboard;
        Shader shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
        if (shader == null)
            shader = Shader.Find("Particles/Standard Unlit");
        if (shader != null)
        {
            var waterColor = new Color(0.35f, 0.85f, 1f, 0.95f);
            var material = new Material(shader)
            {
                name = "Water Droplet Runtime Material",
                color = waterColor
            };
            if (material.HasProperty("_BaseColor"))
                material.SetColor("_BaseColor", waterColor);
            if (material.HasProperty("_Color"))
                material.SetColor("_Color", waterColor);
            renderer.material = material;
        }

        return particles;
    }
}
