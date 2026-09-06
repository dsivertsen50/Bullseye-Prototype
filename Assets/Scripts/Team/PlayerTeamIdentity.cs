using Unity.Netcode;
using UnityEngine;

/// <summary>
/// Optional team assignment hook. Team 0 means free-for-all / unassigned.
/// REQ-053 uses this for ping filtering and does not introduce a full
/// team-mode framework.
/// </summary>
public class PlayerTeamIdentity : NetworkBehaviour
{
    public const int UnassignedTeamId = 0;

    private readonly NetworkVariable<int> teamId = new(
        UnassignedTeamId,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);

    public int TeamId => teamId.Value;
    public bool HasTeam => TeamId != UnassignedTeamId;

    public void SetTeamId(int value)
    {
        if (!IsServer)
            return;

        teamId.Value = value;
    }
}
