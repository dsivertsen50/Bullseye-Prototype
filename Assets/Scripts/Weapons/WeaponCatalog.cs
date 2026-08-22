using UnityEngine;

[CreateAssetMenu(
    fileName = "WeaponCatalog",
    menuName = "Bullseye/Weapons/Weapon Catalog")]
public class WeaponCatalog : ScriptableObject
{
    [SerializeField] private WeaponDefinition[] weapons = System.Array.Empty<WeaponDefinition>();

    public int Count => weapons != null ? weapons.Length : 0;

    public WeaponDefinition Get(int index)
    {
        if (weapons == null || index < 0 || index >= weapons.Length)
            return null;

        return weapons[index];
    }

    public int IndexOf(WeaponDefinition definition)
    {
        if (definition == null || weapons == null)
            return -1;

        for (int i = 0; i < weapons.Length; i++)
        {
            if (weapons[i] == definition)
                return i;
        }

        return IndexOfId(definition.WeaponId);
    }

    public int IndexOfId(string weaponId)
    {
        if (string.IsNullOrEmpty(weaponId) || weapons == null)
            return -1;

        for (int i = 0; i < weapons.Length; i++)
        {
            if (weapons[i] != null && weapons[i].WeaponId == weaponId)
                return i;
        }

        return -1;
    }

    public WeaponDefinition GetById(string weaponId)
    {
        return Get(IndexOfId(weaponId));
    }

    public WeaponDefinition GetPermanentDefault()
    {
        if (weapons == null)
            return null;

        for (int i = 0; i < weapons.Length; i++)
        {
            if (weapons[i] != null && weapons[i].IsPermanentDefault)
                return weapons[i];
        }

        return Get(0);
    }

    private void OnValidate()
    {
        if (weapons == null)
            return;

        for (int i = 0; i < weapons.Length; i++)
        {
            WeaponDefinition current = weapons[i];
            if (current == null)
                continue;

            for (int j = i + 1; j < weapons.Length; j++)
            {
                if (weapons[j] != null && weapons[j].WeaponId == current.WeaponId)
                    Debug.LogWarning($"WeaponCatalog '{name}' has duplicate Weapon ID '{current.WeaponId}'.", this);
            }
        }
    }
}
