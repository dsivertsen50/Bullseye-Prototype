using System.Globalization;
using UnityEngine;

/// <summary>
/// Shared player-facing formatting for career statistics.
/// Values are display-only; gameplay telemetry is never calculated here.
/// </summary>
public static class ProfileStatFormatter
{
    public const string EmDash = "—";

    private static readonly CultureInfo Culture = CultureInfo.InvariantCulture;

    public static string Count(int value)
    {
        return Mathf.Max(0, value).ToString("N0", Culture);
    }

    public static string Count(float value)
    {
        if (float.IsNaN(value) || float.IsInfinity(value))
            return Count(0);

        return Count(Mathf.RoundToInt(Mathf.Max(0f, value)));
    }

    public static string Ratio(float value)
    {
        if (float.IsNaN(value) || float.IsInfinity(value))
            return "0.00";

        return Mathf.Max(0f, value).ToString("0.00", Culture);
    }

    public static string Percent(float fraction)
    {
        if (float.IsNaN(fraction) || float.IsInfinity(fraction))
            return "0.0%";

        float clamped = Mathf.Max(0f, fraction) * 100f;
        return clamped.ToString("0.0", Culture) + "%";
    }

    public static string DistanceMeters(float meters)
    {
        if (float.IsNaN(meters) || float.IsInfinity(meters) || meters <= 0f)
            return EmDash;

        return meters.ToString("0.0", Culture) + " m";
    }

    public static string PlayTime(float totalSeconds)
    {
        if (float.IsNaN(totalSeconds) || float.IsInfinity(totalSeconds) || totalSeconds <= 0f)
            return "0m";

        int seconds = Mathf.FloorToInt(totalSeconds);
        int hours = seconds / 3600;
        int minutes = (seconds % 3600) / 60;
        if (hours > 0)
            return hours.ToString(Culture) + "h " + minutes.ToString(Culture) + "m";

        return minutes.ToString(Culture) + "m";
    }

    public static string FavoriteWeapon(string displayName)
    {
        return string.IsNullOrWhiteSpace(displayName) ? EmDash : displayName.Trim();
    }

    public static string ShortProfileId(string profileId)
    {
        if (string.IsNullOrWhiteSpace(profileId))
            return EmDash;

        string trimmed = profileId.Trim();
        if (trimmed.Length <= 8)
            return trimmed;

        return trimmed.Substring(0, 8) + "...";
    }
}
