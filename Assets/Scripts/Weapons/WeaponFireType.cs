/// <summary>
/// Distinguishes instantaneous hitscan firearms from spawned projectiles.
/// New projectile weapons should use Projectile rather than special-casing
/// a weapon id in player input.
/// </summary>
public enum WeaponFireType
{
    Hitscan = 0,
    Projectile = 1
}
