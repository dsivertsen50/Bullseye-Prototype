using System;
using Unity.Collections;
using Unity.Netcode;

public struct LobbyPlayerNetworkState : INetworkSerializable, IEquatable<LobbyPlayerNetworkState>
{
    public ulong ClientId;
    public FixedString32Bytes DisplayName;
    public FixedString32Bytes PublicTag;
    public bool IsHost;
    public bool IsReady;

    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        serializer.SerializeValue(ref ClientId);
        serializer.SerializeValue(ref DisplayName);
        serializer.SerializeValue(ref PublicTag);
        serializer.SerializeValue(ref IsHost);
        serializer.SerializeValue(ref IsReady);
    }

    public bool Equals(LobbyPlayerNetworkState other)
    {
        return ClientId == other.ClientId;
    }
}
