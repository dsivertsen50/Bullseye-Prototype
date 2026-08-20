using UnityEngine;

[CreateAssetMenu(
    fileName = "WeaponDefinition",
    menuName = "Bullseye/Weapons/Weapon Definition")]
public class WeaponDefinition : ScriptableObject
{
    [SerializeField] private string weaponId = "ruger22";
    [SerializeField] private GameObject firstPersonPrefab;
    [SerializeField] private GameObject worldPrefab;
    [SerializeField] private WeaponPresentationConfig presentation;

    [Header("World Attachment")]
    [SerializeField] private Vector3 worldLocalPosition = new(0.34f, 0f, 0.38f);
    [SerializeField] private Vector3 worldLocalEuler;
    [SerializeField] private Vector3 worldLocalScale = Vector3.one;
    [SerializeField] private float worldStanceHeightOffset = 0.28f;

    public string WeaponId => weaponId;
    public GameObject FirstPersonPrefab => firstPersonPrefab;
    public GameObject WorldPrefab => worldPrefab;
    public WeaponPresentationConfig Presentation => presentation;
    public Vector3 WorldLocalPosition => worldLocalPosition;
    public Vector3 WorldLocalEuler => worldLocalEuler;
    public Vector3 WorldLocalScale => worldLocalScale;
    public float WorldStanceHeightOffset => worldStanceHeightOffset;
}
