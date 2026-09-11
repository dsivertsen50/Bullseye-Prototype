/// <summary>
/// How a spawned projectile steers after launch. REQ-060 implements Straight
/// only. Homing is reserved so a later ticket can add heat-seeking without
/// replacing the projectile prefab or fire pipeline.
/// </summary>
public enum ProjectileGuidanceMode
{
    Straight = 0,
    Homing = 1
}
