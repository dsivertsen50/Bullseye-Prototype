using System;
using Unity.Netcode;
using UnityEngine;

/// <summary>
/// Networked runtime state for one carried or ground weapon instance.
/// Identity is a catalog index, never an object or display name.
/// </summary>
[Serializable]
public struct WeaponRuntimeState : INetworkSerializable, IEquatable<WeaponRuntimeState>
{
    public int CatalogIndex;
    public int Magazine;
    public int Reserve;

    public static WeaponRuntimeState Empty => new()
    {
        CatalogIndex = -1,
        Magazine = 0,
        Reserve = 0
    };

    public bool IsEmpty => CatalogIndex < 0;

    public int TotalAmmo => Mathf.Max(0, Magazine) + Mathf.Max(0, Reserve);

    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        serializer.SerializeValue(ref CatalogIndex);
        serializer.SerializeValue(ref Magazine);
        serializer.SerializeValue(ref Reserve);
    }

    public bool Equals(WeaponRuntimeState other)
    {
        return CatalogIndex == other.CatalogIndex &&
               Magazine == other.Magazine &&
               Reserve == other.Reserve;
    }

    public override bool Equals(object obj)
    {
        return obj is WeaponRuntimeState other && Equals(other);
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(CatalogIndex, Magazine, Reserve);
    }
}
