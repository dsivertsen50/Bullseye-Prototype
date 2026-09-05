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

    [Header("Surface Impact Decals")]
    [SerializeField] private WeaponImpactDecalSettings impactDecalSettings = new();

    [Header("Shot Audio")]
    [SerializeField, Tooltip("Optional clip overrides. Leave empty to use the shared impact and flyby libraries.")]
    private WeaponShotAudioOverrides shotAudioOverrides = new();

    [Header("ADS")]
    [SerializeField, Tooltip("When enabled, ADS applies true camera magnification from Ads Magnification. When disabled, ADS only changes weapon pose and handling.")]
    private bool usesMagnifiedAds = true;
    [SerializeField, Min(1f), Tooltip("Optical magnification while ADS. 1.0x is no zoom. 1.15x is a mild sight picture. 2.5x is the current DMR optic.")]
    private float adsMagnification = 1.15f;
    [SerializeField, Min(0.01f), Tooltip("Seconds to blend into this weapon's ADS camera, sensitivity, and pose.")]
    private float adsEnterDuration = 0.18f;
    [SerializeField, Min(0.01f), Tooltip("Seconds to blend out of this weapon's ADS camera, sensitivity, and pose.")]
    private float adsExitDuration = 0.15f;
    [SerializeField, Range(0.05f, 1.5f), Tooltip("Look scale while ADS, applied after mouse/gamepad sensitivity and the player's ADS Sensitivity setting.")]
    private float adsSensitivityMultiplier = 0.4f;
    [SerializeField, Tooltip("Hides the first-person weapon while ADS so the view is a scope picture rather than looking down the gun. Does not affect third-person.")]
    private bool adsHidesViewmodel;

    [Header("Scope Presentation")]
    [SerializeField, Tooltip("Visual optic overlay while ADS. Independent of Ads Magnification. Leave empty for weapons that only pose-aim.")]
    private ScopeDefinition scopePresentation;

    [Header("Third-Person Socket")]
    [SerializeField, Tooltip("Local position of the world weapon on the right-hand WeaponSocket.")]
    private Vector3 worldLocalPosition;
    [SerializeField, Tooltip("Local rotation of the world weapon on the right-hand WeaponSocket.")]
    private Vector3 worldLocalEuler;
    [SerializeField, Tooltip("Local scale of the world weapon on the right-hand WeaponSocket.")]
    private Vector3 worldLocalScale = Vector3.one;
    [SerializeField] private float worldStanceHeightOffset = 0.28f;

    [Header("Third-Person Hold")]
    [SerializeField] private ThirdPersonWeaponPoseClass weaponPoseClass = ThirdPersonWeaponPoseClass.ShortGun;
    [SerializeField] private Vector3 thirdPersonAnchorPositionOffset;
    [SerializeField] private Vector3 thirdPersonAnchorRotationOffset;
    [SerializeField] private bool useLeftHandGrip = true;
    [SerializeField] private ThirdPersonWeaponHoldProfile optionalHoldProfileOverride;
    [SerializeField] private ThirdPersonWeaponPoseProfile thirdPersonPoseProfile;
    [SerializeField] private bool poseClassAssigned;
    [SerializeField] private ThirdPersonPoseCategory thirdPersonPoseCategory = ThirdPersonPoseCategory.Pistol;
    [SerializeField] private bool supportHandIkEnabled = true;
    [SerializeField, Min(0.01f)] private float ikBlendDuration = 0.12f;
    [SerializeField, Min(0.01f)] private float weaponPoseBlendDuration = 0.14f;
    [SerializeField, Range(0f, 1f)] private float sprintSupportIkWeight = 0.55f;
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
    public WeaponImpactDecalSettings ImpactDecalSettings => impactDecalSettings ??= new WeaponImpactDecalSettings();
    public WeaponShotAudioOverrides ShotAudioOverrides => shotAudioOverrides ??= new WeaponShotAudioOverrides();
    public bool UsesMagnifiedAds => usesMagnifiedAds;
    public float AdsMagnification => usesMagnifiedAds ? Mathf.Max(1f, adsMagnification) : 1f;
    public float AdsEnterDuration => Mathf.Max(0.01f, adsEnterDuration);
    public float AdsExitDuration => Mathf.Max(0.01f, adsExitDuration);
    public float AdsSensitivityMultiplier => Mathf.Clamp(adsSensitivityMultiplier, 0.05f, 1.5f);
    public bool AdsHidesViewmodel => adsHidesViewmodel;
    public ScopeDefinition ScopePresentation => scopePresentation;
    public bool UsesScopeOverlay => scopePresentation != null && scopePresentation.UsesScopeOverlay;
    public Vector3 WorldLocalPosition => worldLocalPosition;
    public Vector3 WorldLocalEuler => worldLocalEuler;
    public Vector3 WorldLocalScale => worldLocalScale;
    public Vector3 ThirdPersonWeaponPositionOffset => thirdPersonAnchorPositionOffset;
    public Vector3 ThirdPersonWeaponRotationOffset => thirdPersonAnchorRotationOffset;
    public Vector3 ThirdPersonAnchorPositionOffset => thirdPersonAnchorPositionOffset;
    public Vector3 ThirdPersonAnchorRotationOffset => thirdPersonAnchorRotationOffset;
    public float WorldStanceHeightOffset => worldStanceHeightOffset;
    public ThirdPersonWeaponPoseClass ThirdPersonHoldClass => WeaponPoseClass;
    public ThirdPersonWeaponHoldProfile HoldProfileOverride => optionalHoldProfileOverride;
    public ThirdPersonWeaponPoseClass WeaponPoseClass
    {
        get
        {
            if (optionalHoldProfileOverride != null)
                return optionalHoldProfileOverride.HoldClass;
            if (thirdPersonPoseProfile != null)
                return thirdPersonPoseProfile.WeaponPoseClass;
            return weaponPoseClass;
        }
    }

    public ThirdPersonWeaponPoseProfile PoseProfile => thirdPersonPoseProfile;
    public ThirdPersonPoseCategory PoseCategory =>
        WeaponPoseClass == ThirdPersonWeaponPoseClass.ShortGun
            ? ThirdPersonPoseCategory.Pistol
            : ThirdPersonPoseCategory.LongGun;
    public bool SupportHandIkEnabled =>
        thirdPersonPoseProfile != null ? thirdPersonPoseProfile.SupportHandIkEnabled : supportHandIkEnabled;
    public float IkBlendDuration =>
        thirdPersonPoseProfile != null
            ? thirdPersonPoseProfile.IkBlendDuration
            : Mathf.Max(0.01f, ikBlendDuration);
    public float WeaponPoseBlendDuration =>
        thirdPersonPoseProfile != null
            ? thirdPersonPoseProfile.WeaponPoseBlendDuration
            : Mathf.Max(0.01f, weaponPoseBlendDuration);
    public float SprintSupportIkWeight =>
        thirdPersonPoseProfile != null
            ? thirdPersonPoseProfile.SprintSupportIkWeight
            : Mathf.Clamp01(sprintSupportIkWeight);
    public bool UseLeftHandGrip =>
        WeaponPoseClass == ThirdPersonWeaponPoseClass.ShortGun
            ? useLeftHandGrip && SupportHandIkEnabled
            : useLeftHandGrip && SupportHandIkEnabled;
    public bool UsesSupportHandIk => UseLeftHandGrip;
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
        impactDecalSettings ??= new WeaponImpactDecalSettings();
        impactDecalSettings.Validate();
        shotAudioOverrides ??= new WeaponShotAudioOverrides();
        adsMagnification = Mathf.Max(1f, adsMagnification);
        adsEnterDuration = Mathf.Max(0.01f, adsEnterDuration);
        adsExitDuration = Mathf.Max(0.01f, adsExitDuration);
        adsSensitivityMultiplier = Mathf.Clamp(adsSensitivityMultiplier, 0.05f, 1.5f);
        ikBlendDuration = Mathf.Max(0.01f, ikBlendDuration);
        weaponPoseBlendDuration = Mathf.Max(0.01f, weaponPoseBlendDuration);
        sprintSupportIkWeight = Mathf.Clamp01(sprintSupportIkWeight);
        thirdPersonPose ??= ThirdPersonWeaponPose.CreateDefault(thirdPersonClass);
        MigratePoseClassIfNeeded();
    }

    public void AssignPoseProfile(ThirdPersonWeaponPoseProfile profile)
    {
        thirdPersonPoseProfile = profile;
        if (profile != null)
        {
            weaponPoseClass = profile.WeaponPoseClass;
            poseClassAssigned = true;
            thirdPersonPoseCategory = profile.WeaponPoseClass == ThirdPersonWeaponPoseClass.ShortGun
                ? ThirdPersonPoseCategory.Pistol
                : ThirdPersonPoseCategory.LongGun;
        }
    }

    public void AssignWeaponPoseClass(ThirdPersonWeaponPoseClass poseClass)
    {
        weaponPoseClass = poseClass;
        poseClassAssigned = true;
        useLeftHandGrip = poseClass != ThirdPersonWeaponPoseClass.ShortGun;
        supportHandIkEnabled = useLeftHandGrip;
        thirdPersonPoseCategory = poseClass == ThirdPersonWeaponPoseClass.ShortGun
            ? ThirdPersonPoseCategory.Pistol
            : ThirdPersonPoseCategory.LongGun;
    }

    public void AssignHoldOverride(ThirdPersonWeaponHoldProfile profile)
    {
        optionalHoldProfileOverride = profile;
        if (profile != null)
            AssignWeaponPoseClass(profile.HoldClass);
    }

    public void AssignAnchorOffsets(Vector3 position, Vector3 euler)
    {
        thirdPersonAnchorPositionOffset = position;
        thirdPersonAnchorRotationOffset = euler;
    }

    private void MigratePoseClassIfNeeded()
    {
        if (poseClassAssigned)
            return;

        if (thirdPersonPoseProfile != null)
        {
            weaponPoseClass = thirdPersonPoseProfile.WeaponPoseClass;
            poseClassAssigned = true;
            return;
        }

        weaponPoseClass = thirdPersonPoseCategory == ThirdPersonPoseCategory.LongGun
            ? ThirdPersonWeaponPoseClass.LongGun
            : ThirdPersonWeaponPoseClass.ShortGun;
        poseClassAssigned = true;
    }
}
