using Unity.Netcode;

/// <summary>
/// Delivers one authoritative combat result to the attacker client.
/// The HUD does not decide who should see the message.
/// </summary>
public class CombatFeedbackRelay : NetworkBehaviour
{
    public void NotifyAttacker(ulong attackerClientId, CombatFeedbackEvent feedback)
    {
        if (!IsServer || !IsSpawned)
            return;

        if (attackerClientId == OwnerClientId || attackerClientId == DamageContext.NoAttackerId)
            return;

        if (NetworkManager == null || !NetworkManager.ConnectedClients.ContainsKey(attackerClientId))
            return;

        DeliverCombatFeedbackRpc(
            feedback.Amount,
            (byte)feedback.Type,
            (byte)feedback.Award,
            (byte)feedback.SecondaryAward,
            (byte)feedback.TertiaryAward,
            feedback.Priority,
            RpcTarget.Single(attackerClientId, RpcTargetUse.Temp));
    }

    [Rpc(SendTo.SpecifiedInParams, InvokePermission = RpcInvokePermission.Server)]
    private void DeliverCombatFeedbackRpc(
        int amount,
        byte feedbackType,
        byte award,
        byte secondaryAward,
        byte tertiaryAward,
        int priority,
        RpcParams rpcParams = default)
    {
        CombatFeedbackHud.PresentLocal(new CombatFeedbackEvent
        {
            Amount = amount,
            Type = (CombatFeedbackType)feedbackType,
            Award = (CombatAwardType)award,
            SecondaryAward = (CombatAwardType)secondaryAward,
            TertiaryAward = (CombatAwardType)tertiaryAward,
            CustomText = null,
            Priority = priority
        });
    }
}
