using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

/// <summary>
/// Per-player near-miss probe. Registers itself so hitscan shots can test
/// proximity without searching the scene every bullet.
/// </summary>
public class NearMissReceiver : MonoBehaviour
{
    private static readonly List<NearMissReceiver> active = new(8);

    [SerializeField, Tooltip("Optional torso / chest transform. Defaults to the body collider center.")]
    private Transform referencePoint;
    [SerializeField] private CapsuleCollider bodyCollider;
    [SerializeField] private PlayerHealth playerHealth;
    [SerializeField] private NetworkObject networkObject;

    public static IReadOnlyList<NearMissReceiver> Active => active;

    public ulong OwnerClientId =>
        networkObject != null && networkObject.IsSpawned
            ? networkObject.OwnerClientId
            : ulong.MaxValue;

    public PlayerHealth Health => playerHealth;

    public Vector3 ReferencePosition
    {
        get
        {
            if (referencePoint != null)
                return referencePoint.position;
            if (bodyCollider != null)
                return bodyCollider.bounds.center;
            return transform.position + Vector3.up;
        }
    }

    public bool IsEligible
    {
        get
        {
            if (!isActiveAndEnabled)
                return false;
            if (networkObject == null || !networkObject.IsSpawned)
                return false;
            if (playerHealth != null && playerHealth.IsDead)
                return false;
            return true;
        }
    }

    private void Awake()
    {
        if (playerHealth == null)
            playerHealth = GetComponent<PlayerHealth>();
        if (networkObject == null)
            networkObject = GetComponent<NetworkObject>();
        if (bodyCollider == null)
            bodyCollider = GetComponentInChildren<CapsuleCollider>();
    }

    private void OnEnable()
    {
        if (!active.Contains(this))
            active.Add(this);
    }

    private void OnDisable()
    {
        active.Remove(this);
    }

    public static float DistanceToSegment(Vector3 point, Vector3 a, Vector3 b, out Vector3 closest)
    {
        Vector3 ab = b - a;
        float lengthSq = ab.sqrMagnitude;
        if (lengthSq < 0.0000001f)
        {
            closest = a;
            return Vector3.Distance(point, a);
        }

        float t = Mathf.Clamp01(Vector3.Dot(point - a, ab) / lengthSq);
        closest = a + ab * t;
        return Vector3.Distance(point, closest);
    }

    public static void EvaluateShot(
        NetworkObject shooter,
        IList<Vector3> origins,
        IList<Vector3> ends,
        IList<PlayerHealth> directHits,
        float radius,
        bool debug,
        List<ulong> clientIds,
        List<Vector3> closestPoints,
        List<float> distances)
    {
        clientIds.Clear();
        closestPoints.Clear();
        distances.Clear();

        if (shooter == null || origins == null || ends == null)
            return;
        if (origins.Count == 0 || radius <= 0f)
            return;

        int segmentCount = Mathf.Min(origins.Count, ends.Count);
        ulong shooterId = shooter.IsSpawned ? shooter.OwnerClientId : ulong.MaxValue;

        if (debug)
        {
            for (int i = 0; i < segmentCount; i++)
                Debug.DrawLine(origins[i], ends[i], Color.yellow, 1.5f);
        }

        for (int i = 0; i < active.Count; i++)
        {
            NearMissReceiver receiver = active[i];
            if (receiver == null || !receiver.IsEligible)
                continue;
            if (receiver.OwnerClientId == shooterId)
                continue;
            if (WasDirectlyHit(receiver, directHits))
            {
                if (debug)
                    Debug.Log($"Near miss skipped: client {receiver.OwnerClientId} was directly hit");
                continue;
            }

            Vector3 probe = receiver.ReferencePosition;
            float bestDistance = float.MaxValue;
            Vector3 bestPoint = probe;
            for (int s = 0; s < segmentCount; s++)
            {
                float distance = DistanceToSegment(probe, origins[s], ends[s], out Vector3 closest);
                if (distance >= bestDistance)
                    continue;
                bestDistance = distance;
                bestPoint = closest;
            }

            bool triggered = bestDistance <= radius;
            if (debug)
            {
                Debug.DrawLine(probe, bestPoint, triggered ? Color.red : Color.gray, 1.5f);
                Debug.DrawRay(bestPoint, Vector3.up * 0.25f, Color.cyan, 1.5f);
                Debug.Log(
                    $"Near miss client {receiver.OwnerClientId} distance {bestDistance:0.00}m " +
                    $"radius {radius:0.00}m triggered={(triggered ? "YES" : "NO")}");
            }

            if (!triggered)
                continue;

            clientIds.Add(receiver.OwnerClientId);
            closestPoints.Add(bestPoint);
            distances.Add(bestDistance);
        }
    }

    private static bool WasDirectlyHit(NearMissReceiver receiver, IList<PlayerHealth> directHits)
    {
        if (receiver == null || directHits == null || directHits.Count == 0)
            return false;

        PlayerHealth health = receiver.Health;
        if (health == null)
            return false;

        for (int i = 0; i < directHits.Count; i++)
        {
            if (directHits[i] == health)
                return true;
        }

        return false;
    }
}
