using Unity.Netcode;
using UnityEngine;

/// <summary>
/// Shared hitscan selection and multi-bounce reflection used by both the
/// authoritative shot and the local bank-shot predictor.
/// </summary>
public static class HitscanRicochet
{
    public const int DefaultMaxRicochets = 3;
    public const float SurfaceOffset = 0.015f;
    public const float MinimumIncidence = 0.012f;
    public const float MinimumRemainingRange = 0.05f;
    public const float SubsequentBounceDecalDelay = 0.045f;

    public struct BounceRecord
    {
        public RaycastHit hit;
        public Vector3 reflectedOrigin;
        public Vector3 reflectedDirection;
        public float traveledDistance;
    }

    public struct TraceResult
    {
        public Vector3 startOrigin;
        public Vector3 startDirection;
        public bool hasBounce;
        public RaycastHit bounceHit;
        public Vector3 bounceOrigin;
        public Vector3 bounceDirection;
        public BounceRecord bounce0;
        public BounceRecord bounce1;
        public BounceRecord bounce2;
        public bool hasFinalHit;
        public RaycastHit finalHit;
        public Vector3 endPoint;
        public float totalDistance;
        public int bounceCount;

        public bool TryGetBounce(int index, out BounceRecord record)
        {
            switch (index)
            {
                case 0:
                    record = bounce0;
                    return bounceCount > 0;
                case 1:
                    record = bounce1;
                    return bounceCount > 1;
                case 2:
                    record = bounce2;
                    return bounceCount > 2;
                default:
                    record = default;
                    return false;
            }
        }
    }

    public static int BuildHitscanMask()
    {
        int mask = Physics.DefaultRaycastLayers;

        int pickupLayer = LayerMask.NameToLayer("WeaponPickup");
        if (pickupLayer >= 0)
            mask &= ~(1 << pickupLayer);

        int debrisLayer = LayerMask.NameToLayer("BullseyeDebris");
        if (debrisLayer >= 0)
            mask &= ~(1 << debrisLayer);

        return mask;
    }

    public static bool TryBuildReflectedRay(
        Vector3 incomingDirection,
        RaycastHit hit,
        float remainingRange,
        out Vector3 origin,
        out Vector3 direction,
        out float range)
    {
        origin = default;
        direction = default;
        range = 0f;

        if (remainingRange < MinimumRemainingRange)
            return false;

        Vector3 incoming = incomingDirection.sqrMagnitude > 0.0001f
            ? incomingDirection.normalized
            : Vector3.forward;
        Vector3 normal = hit.normal.sqrMagnitude > 0.0001f
            ? hit.normal.normalized
            : Vector3.up;

        float incidence = -Vector3.Dot(incoming, normal);
        if (incidence < MinimumIncidence)
            return false;

        Vector3 reflected = Vector3.Reflect(incoming, normal);
        if (reflected.sqrMagnitude < 0.0001f)
            return false;

        reflected.Normalize();
        if (Vector3.Dot(reflected, normal) < MinimumIncidence)
            return false;

        origin = hit.point + normal * SurfaceOffset;
        direction = reflected;
        range = remainingRange;
        return true;
    }

    public static bool Trace(
        Vector3 origin,
        Vector3 direction,
        float maxRange,
        bool allowRicochet,
        int maxRicochets,
        bool excludePlayersFromReflectedRay,
        NetworkObject ignoreOwner,
        RaycastHit[] buffer,
        out TraceResult result)
    {
        result = new TraceResult
        {
            startOrigin = origin,
            startDirection = direction
        };

        if (direction.sqrMagnitude < 0.0001f || maxRange < MinimumRemainingRange || buffer == null || buffer.Length == 0)
        {
            result.endPoint = origin;
            return false;
        }

        direction.Normalize();
        result.startDirection = direction;

        int mask = BuildHitscanMask();
        if (!TrySelectHit(
                origin,
                direction,
                maxRange,
                mask,
                ignoreOwner,
                excludePlayers: false,
                ignoreCollider: null,
                buffer,
                out RaycastHit firstHit))
        {
            result.endPoint = origin + direction * maxRange;
            result.totalDistance = maxRange;
            return false;
        }

        result.hasFinalHit = true;
        result.finalHit = firstHit;
        result.endPoint = firstHit.point;
        result.totalDistance = firstHit.distance;

        int allowedBounces = allowRicochet ? Mathf.Clamp(maxRicochets, 0, DefaultMaxRicochets) : 0;
        Vector3 segmentOrigin = origin;
        Vector3 segmentDirection = direction;
        float traveled = firstHit.distance;
        RaycastHit currentHit = firstHit;

        while (result.bounceCount < allowedBounces)
        {
            if (!CanRicochetFrom(currentHit.collider))
                break;

            float remaining = maxRange - traveled;
            if (!TryBuildReflectedRay(
                    segmentDirection,
                    currentHit,
                    remaining,
                    out Vector3 bounceOrigin,
                    out Vector3 bounceDirection,
                    out float bounceRange))
            {
                break;
            }

            var record = new BounceRecord
            {
                hit = currentHit,
                reflectedOrigin = bounceOrigin,
                reflectedDirection = bounceDirection,
                traveledDistance = traveled
            };
            SetBounce(ref result, result.bounceCount, record);
            result.hasBounce = true;
            if (result.bounceCount == 0)
            {
                result.bounceHit = currentHit;
                result.bounceOrigin = bounceOrigin;
                result.bounceDirection = bounceDirection;
            }
            else
            {
                result.bounceOrigin = bounceOrigin;
                result.bounceDirection = bounceDirection;
            }

            result.bounceCount++;

            segmentOrigin = bounceOrigin;
            segmentDirection = bounceDirection;

            if (!TrySelectHit(
                    bounceOrigin,
                    bounceDirection,
                    bounceRange,
                    mask,
                    ignoreOwner,
                    excludePlayersFromReflectedRay,
                    currentHit.collider,
                    buffer,
                    out RaycastHit nextHit))
            {
                result.hasFinalHit = false;
                result.finalHit = default;
                result.endPoint = bounceOrigin + bounceDirection * bounceRange;
                result.totalDistance = traveled + bounceRange;
                return true;
            }

            result.hasFinalHit = true;
            result.finalHit = nextHit;
            result.endPoint = nextHit.point;
            traveled += nextHit.distance;
            result.totalDistance = traveled;
            currentHit = nextHit;
        }

        return true;
    }

    public static bool IsPlayerOrBullseye(Collider collider)
    {
        if (collider == null)
            return false;

        return collider.GetComponentInParent<BullseyeTarget>() != null
            || collider.GetComponentInParent<PlayerHealth>() != null;
    }

    public static bool TryGetBullseyeTarget(Collider collider, out BullseyeTarget target)
    {
        target = null;
        if (collider == null)
            return false;

        target = collider.GetComponentInParent<BullseyeTarget>();
        return target != null;
    }

    public static void DrawDebug(in TraceResult result, float duration)
    {
        Debug.DrawRay(result.startOrigin, result.startDirection * 4f, Color.cyan, duration);
        Vector3 previous = result.startOrigin;
        for (int i = 0; i < result.bounceCount; i++)
        {
            if (!result.TryGetBounce(i, out BounceRecord bounce))
                continue;

            Color segment = i == 0 ? Color.cyan : Color.yellow;
            Debug.DrawLine(previous, bounce.hit.point, segment, duration);
            Debug.DrawRay(bounce.hit.point, bounce.hit.normal * 0.45f, Color.green, duration);
            previous = bounce.reflectedOrigin;
        }

        Debug.DrawLine(previous, result.endPoint, result.hasBounce ? Color.yellow : Color.cyan, duration);

        if (result.hasFinalHit)
            Debug.DrawRay(result.finalHit.point, result.finalHit.normal * 0.3f, Color.red, duration);
    }

    private static void SetBounce(ref TraceResult result, int index, BounceRecord record)
    {
        switch (index)
        {
            case 0:
                result.bounce0 = record;
                break;
            case 1:
                result.bounce1 = record;
                break;
            case 2:
                result.bounce2 = record;
                break;
        }
    }

    private static bool CanRicochetFrom(Collider collider)
    {
        if (!RicochetSurface.TryGetEnabled(collider, out _))
            return false;

        return !IsPlayerOrBullseye(collider);
    }

    private static bool TrySelectHit(
        Vector3 origin,
        Vector3 direction,
        float range,
        int mask,
        NetworkObject ignoreOwner,
        bool excludePlayers,
        Collider ignoreCollider,
        RaycastHit[] buffer,
        out RaycastHit selectedHit)
    {
        selectedHit = default;
        int hitCount = Physics.RaycastNonAlloc(
            origin,
            direction,
            buffer,
            range,
            mask,
            QueryTriggerInteraction.Collide);

        float worldDistance = float.MaxValue;
        float bullseyeDistance = float.MaxValue;
        float otherDistance = float.MaxValue;
        RaycastHit worldHit = default;
        RaycastHit bullseyeHit = default;
        RaycastHit otherHit = default;
        bool hasWorld = false;
        bool hasBullseye = false;
        bool hasOther = false;

        for (int i = 0; i < hitCount; i++)
        {
            RaycastHit hit = buffer[i];
            if (hit.collider == null || hit.collider == ignoreCollider)
                continue;

            if (IsOwnCollider(hit.collider, ignoreOwner))
                continue;

            if (IsLocomotionCollider(hit.collider))
                continue;

            if (TryGetBullseyeTarget(hit.collider, out _))
            {
                if (excludePlayers || hit.distance >= bullseyeDistance)
                    continue;

                bullseyeDistance = hit.distance;
                bullseyeHit = hit;
                hasBullseye = true;
                continue;
            }

            if (hit.collider.GetComponentInParent<PlayerHealth>() != null)
            {
                if (excludePlayers || hit.distance >= otherDistance)
                    continue;

                otherDistance = hit.distance;
                otherHit = hit;
                hasOther = true;
                continue;
            }

            if (hit.distance >= worldDistance)
                continue;

            worldDistance = hit.distance;
            worldHit = hit;
            hasWorld = true;
        }

        if (hasBullseye && bullseyeDistance <= worldDistance)
        {
            selectedHit = bullseyeHit;
            return true;
        }

        if (hasOther && otherDistance <= worldDistance)
        {
            selectedHit = otherHit;
            return true;
        }

        if (hasWorld)
        {
            selectedHit = worldHit;
            return true;
        }

        return false;
    }

    private static bool IsLocomotionCollider(Collider collider)
    {
        return collider != null && collider.GetComponent<PlayerLocomotionCollider>() != null;
    }

    private static bool IsOwnCollider(Collider collider, NetworkObject owner)
    {
        if (collider == null || owner == null)
            return false;

        if (TryGetBullseyeTarget(collider, out BullseyeTarget target) &&
            target.OwnerHealth != null &&
            target.OwnerHealth.NetworkObject == owner)
        {
            return true;
        }

        NetworkObject ownerObject = collider.GetComponentInParent<NetworkObject>();
        return ownerObject != null && ownerObject == owner;
    }
}
