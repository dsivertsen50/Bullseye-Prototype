using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Reusable weapon career card populated from WeaponLifetimeStats.
/// Artwork/icons can be added later without changing the bind API.
/// </summary>
public class WeaponProfileEntry : MonoBehaviour
{
    public Button FocusTarget { get; private set; }

    private Text nameLabel;
    private ProfileStatRow eliminations;
    private ProfileStatRow shotsFired;
    private ProfileStatRow shotsHit;
    private ProfileStatRow accuracy;
    private ProfileStatRow damage;
    private ProfileStatRow bullseyeHits;
    private ProfileStatRow headHits;
    private ProfileStatRow bodyHits;
    private ProfileStatRow longest;

    public void BindWidgets(
        Button focusTarget,
        Text weaponName,
        ProfileStatRow eliminationsRow,
        ProfileStatRow shotsFiredRow,
        ProfileStatRow shotsHitRow,
        ProfileStatRow accuracyRow,
        ProfileStatRow damageRow,
        ProfileStatRow bullseyeRow,
        ProfileStatRow headRow,
        ProfileStatRow bodyRow,
        ProfileStatRow longestRow)
    {
        FocusTarget = focusTarget;
        nameLabel = weaponName;
        eliminations = eliminationsRow;
        shotsFired = shotsFiredRow;
        shotsHit = shotsHitRow;
        accuracy = accuracyRow;
        damage = damageRow;
        bullseyeHits = bullseyeRow;
        headHits = headRow;
        bodyHits = bodyRow;
        longest = longestRow;
    }

    public void Bind(WeaponLifetimeStats stats)
    {
        if (stats == null)
            return;

        if (nameLabel != null)
            nameLabel.text = WeaponDisplayNames.Get(stats.WeaponId);

        eliminations?.Set("Eliminations", ProfileStatFormatter.Count(stats.Eliminations));
        shotsFired?.Set("Shots Fired", ProfileStatFormatter.Count(stats.ShotsFired));
        shotsHit?.Set("Shots Hit", ProfileStatFormatter.Count(stats.ShotsHit));
        accuracy?.Set("Accuracy", ProfileStatFormatter.Percent(stats.Accuracy));
        damage?.Set("Damage Dealt", ProfileStatFormatter.Count(stats.DamageDealt));
        bullseyeHits?.Set("Bullseye Hits", ProfileStatFormatter.Count(stats.BullseyeHits));
        headHits?.Set("Head Hits", ProfileStatFormatter.Count(stats.HeadHits));
        bodyHits?.Set("Body Hits", ProfileStatFormatter.Count(stats.BodyHits));
        longest?.Set("Longest Elimination", ProfileStatFormatter.DistanceMeters(stats.LongestEliminationDistance));
    }
}
