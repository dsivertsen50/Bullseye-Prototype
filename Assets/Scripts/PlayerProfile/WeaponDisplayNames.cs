using System.Globalization;
using UnityEngine;

/// <summary>
/// Maps stable WeaponDefinition.WeaponId values to player-facing names.
/// Falls back to a readable form of the id if the catalog is unavailable.
/// </summary>
public static class WeaponDisplayNames
{
    private static WeaponCatalog catalog;

    public static void SetCatalog(WeaponCatalog value)
    {
        catalog = value;
    }

    public static string Get(string weaponId)
    {
        if (string.IsNullOrWhiteSpace(weaponId))
            return "Unknown";

        string id = weaponId.Trim();
        if (catalog != null)
        {
            WeaponDefinition definition = catalog.GetById(id);
            if (definition != null && !string.IsNullOrWhiteSpace(definition.DisplayName))
                return definition.DisplayName;
        }

        return Humanize(id);
    }

    public static string FavoriteWeapon(PlayerProfile profile)
    {
        if (profile == null)
            return ProfileStatFormatter.EmDash;

        string weaponId = profile.FavoriteWeaponId;
        if (string.IsNullOrWhiteSpace(weaponId))
            return ProfileStatFormatter.EmDash;

        return ProfileStatFormatter.FavoriteWeapon(Get(weaponId));
    }

    private static string Humanize(string weaponId)
    {
        string id = weaponId;
        const string prefix = "weapon_";
        if (id.StartsWith(prefix))
            id = id.Substring(prefix.Length);

        id = id.Replace('_', ' ').Trim();
        if (id.Length == 0)
            return "Unknown";

        if (id.Length <= 4 && IsLettersOrDigits(id))
            return id.ToUpperInvariant();

        return CultureInfo.InvariantCulture.TextInfo.ToTitleCase(id.ToLowerInvariant());
    }

    private static bool IsLettersOrDigits(string value)
    {
        for (int i = 0; i < value.Length; i++)
        {
            if (!char.IsLetterOrDigit(value[i]))
                return false;
        }

        return true;
    }
}
