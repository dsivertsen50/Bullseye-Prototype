/// <summary>
/// Kind of combat feedback. New presentation categories can be added here
/// without changing the HUD layout code.
/// </summary>
public enum CombatFeedbackType
{
    Damage = 0,
    Elimination = 1,
    Award = 2
}

/// <summary>
/// Named combat awards. Add a value and a label entry to extend the set.
/// </summary>
public enum CombatAwardType
{
    None = 0,
    Elimination = 1,
    DolphinDive = 2,
    DetachedBullseye = 3,
    Ricochet = 4
}

/// <summary>
/// Authoritative gameplay result for the local combat-feedback HUD.
/// The HUD displays this; it does not calculate damage or classify awards.
/// </summary>
public struct CombatFeedbackEvent
{
    public int Amount;
    public CombatFeedbackType Type;
    public CombatAwardType Award;
    public CombatAwardType SecondaryAward;
    public CombatAwardType TertiaryAward;
    public string CustomText;
    public int Priority;

    public bool IsPlainDamage =>
        Type == CombatFeedbackType.Damage &&
        Award == CombatAwardType.None &&
        SecondaryAward == CombatAwardType.None &&
        TertiaryAward == CombatAwardType.None &&
        string.IsNullOrEmpty(CustomText);

    public static CombatFeedbackEvent FromResolvedDamage(
        DamageContext context,
        int amount,
        bool elimination,
        BullseyeCombatState bullseyeState)
    {
        var feedback = new CombatFeedbackEvent
        {
            Amount = amount,
            Type = CombatFeedbackType.Damage,
            Award = CombatAwardType.None,
            SecondaryAward = CombatAwardType.None,
            TertiaryAward = CombatAwardType.None,
            CustomText = null,
            Priority = 0
        };

        if (context.WasRicochet)
            AddAward(ref feedback, CombatAwardType.Ricochet);

        if (!elimination)
            return feedback;

        feedback.Type = CombatFeedbackType.Elimination;
        feedback.Priority = 1;

        if (context.SourceType == DamageSourceType.BodySlam)
            AddAward(ref feedback, CombatAwardType.DolphinDive);

        if (bullseyeState == BullseyeCombatState.Detached)
            AddAward(ref feedback, CombatAwardType.DetachedBullseye);

        if (feedback.Award != CombatAwardType.None)
        {
            feedback.Type = CombatFeedbackType.Award;
            feedback.Priority = 2;
        }

        return feedback;
    }

    private static void AddAward(ref CombatFeedbackEvent feedback, CombatAwardType award)
    {
        if (award == CombatAwardType.None)
            return;

        if (feedback.Award == CombatAwardType.None)
            feedback.Award = award;
        else if (feedback.SecondaryAward == CombatAwardType.None)
            feedback.SecondaryAward = award;
        else if (feedback.TertiaryAward == CombatAwardType.None)
            feedback.TertiaryAward = award;
    }
}
