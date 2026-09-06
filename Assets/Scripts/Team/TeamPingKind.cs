/// <summary>
/// Lightweight ping classification. Room is left for future callout types
/// without changing the current warning-only behavior.
/// </summary>
public enum TeamPingKind : byte
{
    Location = 0,
    Enemy = 1
}
