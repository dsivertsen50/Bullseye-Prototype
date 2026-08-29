using UnityEngine;

public enum WeaponInventoryRole
{
    TemporaryPickup = 0,
    PermanentDefault = 1
}

[CreateAssetMenu(
    fileName = "WeaponDefinition",
    menuName = "Bullseye/Weapons/Weapon Definition")]
public class WeaponDefinition : ScriptableObject
{
    [SerializeField] private string weaponId = "pistol";
    [SerializeField] private string displayName = "Weapon";
    [SerializeField] private WeaponInventoryRole inventoryRole = WeaponInventoryRole.TemporaryPickup;
    [SerializeField] private GameObject firstPersonPrefab;
    [SerializeField] private GameObject worldPrefab;
    [SerializeField] private GameObject pickupPrefab;
    [SerializeField] private WeaponPresentationConfig presentation;

    [Header("Ammo")]
    [SerializeField] private int magazineSize = 10;
    [SerializeField] private int startingMagazineAmmo = 10;
    [SerializeField] private int startingReserveAmmo = 20;
    [SerializeField] private int maximumReserveAmmo = 60;
    [SerializeField] private bool unlimitedReserve;

    [Header("Fire")]
    [SerializeField] private float fireRate = 0.15f;
    [SerializeField] private bool automatic;
    [SerializeField] private float reloadTime = 1.4f;

    [Header("Damage")]
    [SerializeField] private WeaponDamageSettings damageSettings = new();

    [Header("Accuracy / Reticle")]
    [SerializeField] private WeaponAccuracySettings accuracy = new();

    [Header("World Attachment")]
    [SerializeField] private Vector3 worldLocalPosition = new(0.34f, 0f, 0.38f);
    [SerializeField] private Vector3 worldLocalEuler;
    [SerializeField] private Vector3 worldLocalScale = Vector3.one;
    [SerializeField] private float worldStanceHeightOffset = 0.28f;

    [Header("Third-Person Pose")]
    [SerializeField] private ThirdPersonWeaponClass thirdPersonClass = ThirdPersonWeaponClass.Pistol;
    [SerializeField] private ThirdPersonWeaponPose thirdPersonPose;

    [Header("Ground Pickup Pose")]
    [SerializeField] private Vector3 pickupLocalPosition = new(0f, 0.08f, 0f);
    [SerializeField] private Vector3 pickupLocalEuler = new(0f, 35f, 90f);
    [SerializeField] private Vector3 pickupLocalScale = Vector3.one;
    [SerializeField, Tooltip("If enabled, the dropped-weapon BoxCollider is sized to this mesh after the pickup pose is applied.")]
    private bool fitPickupColliderToMesh = true;
    [SerializeField, Tooltip("Used only when Fit Pickup Collider To Mesh is off.")]
    private Vector3 pickupPhysicsBoxSize;
    [SerializeField] private Vector3 pickupPhysicsBoxCenter;
    [SerializeField, Tooltip("Keeps a very thin mesh from falling through the floor. Extra height is added above the mesh, not below it.")]
    private float pickupColliderMinHeight = 0.05f;

    public string WeaponId => string.IsNullOrWhiteSpace(weaponId) ? name : weaponId;
    public WeaponInventoryRole InventoryRole => inventoryRole;
    public bool IsPermanentDefault => inventoryRole == WeaponInventoryRole.PermanentDefault;
    public bool HasUnlimitedReserve => unlimitedReserve || IsPermanentDefault;
    public string DisplayName
    {
        get
        {
            if (!string.IsNullOrWhiteSpace(displayName))
                return displayName;
            if (presentation != null && !string.IsNullOrWhiteSpace(presentation.WeaponName))
                return presentation.WeaponName;
            return WeaponId;
        }
    }

    public GameObject FirstPersonPrefab => firstPersonPrefab;
    public GameObject WorldPrefab => worldPrefab;
    public GameObject PickupPrefab => pickupPrefab != null ? pickupPrefab : worldPrefab;
    public WeaponPresentationConfig Presentation => presentation;
    public int MagazineSize => Mathf.Max(1, magazineSize);
    public int StartingMagazineAmmo => Mathf.Clamp(startingMagazineAmmo, 0, MagazineSize);
    public int StartingReserveAmmo => Mathf.Clamp(startingReserveAmmo, 0, MaximumReserveAmmo);
    public int MaximumReserveAmmo => Mathf.Max(0, maximumReserveAmmo);
    public float FireRate => Mathf.Max(0.01f, fireRate);
    public bool Automatic => automatic;
    public float ReloadTime => Mathf.Max(0.05f, reloadTime);
    public WeaponDamageSettings DamageSettings => damageSettings ??= new WeaponDamageSettings();
    public WeaponAccuracySettings Accuracy => accuracy ??= new WeaponAccuracySettings();
    public Vector3 WorldLocalPosition => worldLocalPosition;
    public Vector3 WorldLocalEuler => worldLocalEuler;
    public Vector3 WorldLocalScale => worldLocalScale;
    public float WorldStanceHeightOffset => worldStanceHeightOffset;
    public ThirdPersonWeaponClass ThirdPersonClass => thirdPersonClass;
    public ThirdPersonWeaponPose ThirdPersonPose => thirdPersonPose ??= ThirdPersonWeaponPose.CreateDefault(thirdPersonClass);
    public Vector3 PickupLocalPosition => pickupLocalPosition;
    public Vector3 PickupLocalEuler => pickupLocalEuler;
    public Vector3 PickupLocalScale => pickupLocalScale;
    public bool FitPickupColliderToMesh => fitPickupColliderToMesh;
    public Vector3 PickupPhysicsBoxSize => pickupPhysicsBoxSize;
    public Vector3 PickupPhysicsBoxCenter => pickupPhysicsBoxCenter;
    public float PickupColliderMinHeight => Mathf.Max(0.02f, pickupColliderMinHeight);

    public int StartingTotalAmmo => StartingMagazineAmmo + StartingReserveAmmo;

    private void OnValidate()
    {
        magazineSize = Mathf.Max(1, magazineSize);
        maximumReserveAmmo = Mathf.Max(0, maximumReserveAmmo);
        startingMagazineAmmo = Mathf.Clamp(startingMagazineAmmo, 0, magazineSize);
        startingReserveAmmo = Mathf.Clamp(startingReserveAmmo, 0, maximumReserveAmmo);
        fireRate = Mathf.Max(0.01f, fireRate);
        reloadTime = Mathf.Max(0.05f, reloadTime);
        damageSettings ??= new WeaponDamageSettings();
        damageSettings.Validate();
        accuracy ??= new WeaponAccuracySettings();
        accuracy.Validate();
        thirdPersonPose ??= ThirdPersonWeaponPose.CreateDefault(thirdPersonClass);
    }
}
