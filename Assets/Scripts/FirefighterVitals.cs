using UnityEngine;

[RequireComponent(typeof(CharacterController))]
public class FirefighterVitals : MonoBehaviour
{
    [Header("Health")]
    [SerializeField] private float maxHealth = 100f;
    [SerializeField] private float fireDamagePerSecond = 10f;

    [Header("Water Tank")]
    [SerializeField] private float maxWater = 100f;
    [SerializeField] private float waterRechargePerSecond = 7.5f;

    private CharacterController characterController;
    private float currentHealth;
    private float currentWater;
    private bool weaponTriggerHeld;

    public float CurrentHealth => currentHealth;
    public float MaxHealth => maxHealth;
    public float Health01 => maxHealth > 0f ? currentHealth / maxHealth : 0f;
    public float CurrentWater => currentWater;
    public float MaxWater => maxWater;
    public float Water01 => maxWater > 0f ? currentWater / maxWater : 0f;
    public bool IsInFire { get; private set; }

    private void Awake()
    {
        characterController = GetComponent<CharacterController>();
        currentHealth = Mathf.Max(0f, maxHealth);
        currentWater = Mathf.Max(0f, maxWater);
    }

    private void Update()
    {
        IsInFire = IsTouchingFire();
        if (IsInFire)
            currentHealth = Mathf.Max(0f, currentHealth - fireDamagePerSecond * Time.deltaTime);

        if (!weaponTriggerHeld)
            currentWater = Mathf.Min(maxWater, currentWater + waterRechargePerSecond * Time.deltaTime);
    }

    public void SetWeaponTriggerHeld(bool held)
    {
        weaponTriggerHeld = held;
    }

    public bool TryConsumeWater(float amount)
    {
        if (amount <= 0f)
            return true;

        if (currentWater < amount)
            return false;

        currentWater -= amount;
        return true;
    }

    private bool IsTouchingFire()
    {
        if (characterController == null)
            return false;

        Vector3 capsuleCenter = transform.TransformPoint(characterController.center);
        float capsuleRadius = characterController.radius;
        float halfSegment = Mathf.Max(0f, characterController.height * 0.5f - capsuleRadius);
        Vector3 capsuleBottom = capsuleCenter + Vector3.down * halfSegment;
        Vector3 capsuleTop = capsuleCenter + Vector3.up * halfSegment;
        return FireSpreadSystem.IsCapsuleInFire(capsuleBottom, capsuleTop, capsuleRadius);
    }
}
