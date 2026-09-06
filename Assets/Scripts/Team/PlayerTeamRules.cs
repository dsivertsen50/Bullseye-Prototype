using Unity.Netcode;

/// <summary>
/// Shared ally/enemy checks that stay valid in FFA (no teammates) and
/// later team assignments without forcing a game-mode redesign.
/// </summary>
public static class PlayerTeamRules
{
    public static int GetTeamId(NetworkObject playerObject)
    {
        if (playerObject != null && playerObject.TryGetComponent(out PlayerTeamIdentity identity))
            return identity.TeamId;

        return PlayerTeamIdentity.UnassignedTeamId;
    }

    public static int GetTeamIdForClient(NetworkManager networkManager, ulong clientId)
    {
        if (networkManager == null || networkManager.SpawnManager == null)
            return PlayerTeamIdentity.UnassignedTeamId;

        return GetTeamId(networkManager.SpawnManager.GetPlayerNetworkObject(clientId));
    }

    public static bool AreAllies(NetworkManager networkManager, ulong firstClientId, ulong secondClientId)
    {
        if (firstClientId == secondClientId)
            return true;

        int firstTeam = GetTeamIdForClient(networkManager, firstClientId);
        int secondTeam = GetTeamIdForClient(networkManager, secondClientId);
        return firstTeam != PlayerTeamIdentity.UnassignedTeamId && firstTeam == secondTeam;
    }

    public static bool CanReceiveTeamPing(NetworkManager networkManager, ulong ownerClientId, ulong recipientClientId)
    {
        return AreAllies(networkManager, ownerClientId, recipientClientId);
    }
}
