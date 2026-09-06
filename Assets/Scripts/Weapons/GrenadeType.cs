/// <summary>
/// Equipped throwable type. Add new values when introducing more grenades.
/// </summary>
public enum GrenadeType : byte
{
    Standard = 0,
    Suction = 1
}

public static class GrenadeTypeNames
{
    public static string DisplayName(GrenadeType type)
    {
        switch (type)
        {
            case GrenadeType.Suction:
                return "Suction";
            default:
                return "Standard";
        }
    }
}
