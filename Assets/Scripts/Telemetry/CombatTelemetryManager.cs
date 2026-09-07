using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Server-authoritative combat event recorder and per-match statistics.
/// Gameplay systems report confirmed outcomes here; this component does not
/// apply damage or decide combat results.
/// </summary>
public class CombatTelemetryManager : MonoBehaviour
{
    public const int MaxStoredEvents = 2048;
    public const Key DebugOverlayKey = Key.F8;

    private static CombatTelemetryManager instance;

    [SerializeField] private bool showDebugOverlay;
    [SerializeField] private bool logEvents;

    private readonly List<CombatEvent> events = new(256);
    private readonly Dictionary<ulong, PlayerMatchStats> statsByClient = new();
    private readonly List<ulong> assistScratch = new(8);

    private double matchStartServerTime;
    private bool matchClockStarted;
    private bool overlayKeyWasDown;
    private GUIStyle overlayStyle;
    private Vector2 overlayScroll;

    public static CombatTelemetryManager Instance => instance;

    public IReadOnlyList<CombatEvent> Events => events;

    public float MatchTimeSeconds
    {
        get
        {
            if (NetworkManager.Singleton == null)
                return 0f;

            return (float)(NetworkManager.Singleton.ServerTime.Time - matchStartServerTime);
        }
    }

    public bool IsAuthoritative =>
        NetworkManager.Singleton != null && NetworkManager.Singleton.IsServer;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        instance = null;
    }

    public static CombatTelemetryManager Ensure()
    {
        if (instance != null)
            return instance;

        instance = FindAnyObjectByType<CombatTelemetryManager>();
        if (instance != null)
        {
            instance.BindNetwork();
            return instance;
        }

        var go = new GameObject("CombatTelemetryManager");
        if (Application.isPlaying)
            DontDestroyOnLoad(go);

        instance = go.AddComponent<CombatTelemetryManager>();
        instance.BindNetwork();
        return instance;
    }

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        if (Application.isPlaying)
            DontDestroyOnLoad(gameObject);

        BindNetwork();
    }

    private void OnEnable()
    {
        BindNetwork();
    }

    private void OnDestroy()
    {
        UnbindNetwork();
        if (instance == this)
            instance = null;
    }

    private void Update()
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard == null)
            return;

        bool down = keyboard[DebugOverlayKey].isPressed;
        if (down && !overlayKeyWasDown)
            showDebugOverlay = !showDebugOverlay;

        overlayKeyWasDown = down;
    }

    public PlayerMatchStats GetOrCreateStats(ulong clientId)
    {
        if (!statsByClient.TryGetValue(clientId, out PlayerMatchStats stats))
        {
            stats = new PlayerMatchStats { ClientId = clientId };
            statsByClient[clientId] = stats;
        }

        return stats;
    }

    public bool TryGetStats(ulong clientId, out PlayerMatchStats stats)
    {
        return statsByClient.TryGetValue(clientId, out stats);
    }

    public void ResetMatch()
    {
        events.Clear();
        statsByClient.Clear();
        matchClockStarted = true;
        matchStartServerTime = NetworkManager.Singleton != null
            ? NetworkManager.Singleton.ServerTime.Time
            : 0d;

        PlayerStats[] players = FindObjectsByType<PlayerStats>(FindObjectsInactive.Exclude);
        for (int i = 0; i < players.Length; i++)
        {
            if (players[i] != null)
                players[i].ResetMatchStats();
        }

        PlayerHealth[] healths = FindObjectsByType<PlayerHealth>(FindObjectsInactive.Exclude);
        for (int i = 0; i < healths.Length; i++)
        {
            if (healths[i] != null)
                healths[i].ClearAssistContributors();
        }

        if (logEvents)
            Debug.Log("Combat telemetry match reset.");
    }

    public void RecordShotFired(ulong shooterClientId, string weaponId)
    {
        if (!BeginRecord())
            return;

        PlayerMatchStats stats = GetOrCreateStats(shooterClientId);
        stats.ShotsFired++;

        AddEvent(new CombatEvent
        {
            Type = CombatEventType.ShotFired,
            ActorClientId = shooterClientId,
            WeaponId = NormalizeWeaponId(weaponId)
        });
    }

    public void RecordBullseyeHit(
        ulong attackerClientId,
        ulong victimClientId,
        string weaponId,
        float distance,
        BullseyeCombatState bullseyeState,
        int damageAmount)
    {
        if (!BeginRecord())
            return;

        PlayerMatchStats stats = GetOrCreateStats(attackerClientId);
        stats.BullseyeHits++;

        AddEvent(new CombatEvent
        {
            Type = CombatEventType.BullseyeHit,
            ActorClientId = attackerClientId,
            TargetClientId = victimClientId,
            SourceType = EliminationSourceType.Firearm,
            WeaponId = NormalizeWeaponId(weaponId),
            Distance = Mathf.Max(0f, distance),
            BullseyeState = bullseyeState,
            Amount = Mathf.Max(0, damageAmount)
        });

        if (damageAmount > 0)
        {
            AddEvent(new CombatEvent
            {
                Type = CombatEventType.BullseyeDamaged,
                ActorClientId = attackerClientId,
                TargetClientId = victimClientId,
                SourceType = EliminationSourceType.Firearm,
                WeaponId = NormalizeWeaponId(weaponId),
                Distance = Mathf.Max(0f, distance),
                BullseyeState = bullseyeState,
                Amount = damageAmount
            });
        }
    }

    public void RecordBullseyeDetached(
        ulong victimClientId,
        ulong causerClientId,
        BullseyeDetachMethod method,
        Vector3 worldPosition)
    {
        if (!BeginRecord())
            return;

        if (causerClientId != DamageContext.NoAttackerId)
        {
            PlayerMatchStats causer = GetOrCreateStats(causerClientId);
            causer.BullseyesDetached++;
            if (!causer.DetachmentsByMethod.TryAdd(method, 1))
                causer.DetachmentsByMethod[method]++;
        }

        AddEvent(new CombatEvent
        {
            Type = CombatEventType.BullseyeDetached,
            ActorClientId = causerClientId,
            TargetClientId = victimClientId,
            DetachMethod = method,
            WorldPosition = worldPosition
        });
    }

    public void RecordElimination(
        DamageContext context,
        ulong victimClientId,
        BullseyeCombatState bullseyeState,
        Vector3 bullseyePosition,
        IReadOnlyList<ulong> assistContributors)
    {
        if (!BeginRecord())
            return;

        PlayerMatchStats victim = GetOrCreateStats(victimClientId);
        victim.Deaths++;

        PlayerStats victimNetworkStats = PlayerStats.FindOwnedByClient(victimClientId);
        if (victimNetworkStats != null)
            victimNetworkStats.AddDeath();

        AddEvent(new CombatEvent
        {
            Type = CombatEventType.PlayerDeath,
            TargetClientId = victimClientId,
            SourceType = ToEliminationSource(context.SourceType),
            WeaponId = NormalizeWeaponId(context.SourceId),
            BullseyeState = bullseyeState,
            Distance = Mathf.Max(0f, context.Distance),
            WorldPosition = bullseyePosition
        });

        ulong attackerId = context.HasAttacker ? context.AttackerClientId : DamageContext.NoAttackerId;
        bool creditElimination = context.HasAttacker && attackerId != victimClientId;
        if (creditElimination)
        {
            PlayerMatchStats attacker = GetOrCreateStats(attackerId);
            attacker.Eliminations++;
            Increment(attacker.EliminationsBySource, ToEliminationSource(context.SourceType));

            if (bullseyeState == BullseyeCombatState.Detached)
                attacker.DetachedBullseyeEliminations++;
            else
                attacker.AttachedBullseyeEliminations++;

            if (context.SourceType == DamageSourceType.BodySlam)
            {
                attacker.BodySlamEliminations++;
            }
            else if (context.SourceType == DamageSourceType.Firearm)
            {
                string weaponId = NormalizeWeaponId(context.SourceId);
                if (!string.IsNullOrEmpty(weaponId))
                    Increment(attacker.EliminationsByWeapon, weaponId);

                attacker.FirearmEliminationDistances.Add(Mathf.Max(0f, context.Distance));
            }

            PlayerStats attackerNetworkStats = PlayerStats.FindOwnedByClient(attackerId);
            if (attackerNetworkStats != null)
            {
                attackerNetworkStats.AddElimination();
                if (bullseyeState == BullseyeCombatState.Detached)
                    attackerNetworkStats.AddDetachedBullseyeElimination();
            }
        }

        AddEvent(new CombatEvent
        {
            Type = CombatEventType.Elimination,
            ActorClientId = creditElimination ? attackerId : DamageContext.NoAttackerId,
            TargetClientId = victimClientId,
            SourceType = ToEliminationSource(context.SourceType),
            WeaponId = context.SourceType == DamageSourceType.Firearm
                ? NormalizeWeaponId(context.SourceId)
                : CombatEvent.NoWeaponId,
            BullseyeState = bullseyeState,
            Distance = Mathf.Max(0f, context.Distance),
            WorldPosition = bullseyePosition
        });

        AwardAssists(victimClientId, attackerId, assistContributors);
    }

    public void DumpToLog()
    {
        Debug.Log($"Combat telemetry: {events.Count} events, {statsByClient.Count} players, t={MatchTimeSeconds:0.00}s");
        for (int i = 0; i < events.Count; i++)
            Debug.Log(events[i].Format());
    }

    private void AwardAssists(
        ulong victimClientId,
        ulong eliminatingClientId,
        IReadOnlyList<ulong> assistContributors)
    {
        if (assistContributors == null || assistContributors.Count == 0)
            return;

        assistScratch.Clear();
        for (int i = 0; i < assistContributors.Count; i++)
        {
            ulong contributor = assistContributors[i];
            if (contributor == victimClientId || contributor == eliminatingClientId)
                continue;

            if (contributor == DamageContext.NoAttackerId)
                continue;

            if (assistScratch.Contains(contributor))
                continue;

            assistScratch.Add(contributor);

            PlayerMatchStats stats = GetOrCreateStats(contributor);
            stats.Assists++;

            PlayerStats networkStats = PlayerStats.FindOwnedByClient(contributor);
            if (networkStats != null)
                networkStats.AddAssist();

            AddEvent(new CombatEvent
            {
                Type = CombatEventType.AssistAwarded,
                ActorClientId = contributor,
                TargetClientId = victimClientId
            });
        }
    }

    private bool BeginRecord()
    {
        if (!IsAuthoritative)
            return false;

        EnsureMatchClock();
        return true;
    }

    private void AddEvent(CombatEvent combatEvent)
    {
        combatEvent.MatchTime = MatchTimeSeconds;
        events.Add(combatEvent);
        if (events.Count > MaxStoredEvents)
            events.RemoveAt(0);

        if (logEvents)
            Debug.Log(combatEvent.Format());
    }

    private void EnsureMatchClock()
    {
        if (matchClockStarted)
            return;

        matchClockStarted = true;
        matchStartServerTime = NetworkManager.Singleton != null
            ? NetworkManager.Singleton.ServerTime.Time
            : 0d;
    }

    private void BindNetwork()
    {
        NetworkManager networkManager = NetworkManager.Singleton;
        if (networkManager == null)
            return;

        networkManager.OnServerStarted -= HandleServerStarted;
        networkManager.OnServerStarted += HandleServerStarted;

        if (networkManager.IsServer)
            EnsureMatchClock();
    }

    private void UnbindNetwork()
    {
        NetworkManager networkManager = NetworkManager.Singleton;
        if (networkManager == null)
            return;

        networkManager.OnServerStarted -= HandleServerStarted;
    }

    private void HandleServerStarted()
    {
        matchClockStarted = false;
        ResetMatch();
    }

    private static void Increment<T>(Dictionary<T, int> counts, T key)
    {
        if (!counts.TryAdd(key, 1))
            counts[key]++;
    }

    private static string NormalizeWeaponId(string weaponId)
    {
        return string.IsNullOrWhiteSpace(weaponId) ? CombatEvent.NoWeaponId : weaponId;
    }

    public static EliminationSourceType ToEliminationSource(DamageSourceType sourceType)
    {
        return sourceType switch
        {
            DamageSourceType.Firearm => EliminationSourceType.Firearm,
            DamageSourceType.BodySlam => EliminationSourceType.BodySlam,
            DamageSourceType.Grenade => EliminationSourceType.Grenade,
            DamageSourceType.Environment => EliminationSourceType.Environmental,
            _ => EliminationSourceType.Other
        };
    }

    public static BullseyeCombatState ResolveBullseyeState(BullseyeDetachController detachController)
    {
        if (detachController == null || detachController.IsAttached)
            return BullseyeCombatState.Attached;

        return BullseyeCombatState.Detached;
    }

    private void OnGUI()
    {
        if (!showDebugOverlay)
            return;

        if (overlayStyle == null)
        {
            overlayStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 13,
                richText = false,
                wordWrap = true
            };
            overlayStyle.normal.textColor = Color.white;
        }

        const float width = 520f;
        const float height = 360f;
        var window = new Rect(24f, 136f, width, height);
        GUI.Box(window, GUIContent.none);
        var view = new Rect(window.x + 8f, window.y + 8f, width - 16f, height - 16f);
        GUILayout.BeginArea(view);
        overlayScroll = GUILayout.BeginScrollView(overlayScroll);

        GUILayout.Label($"Combat Telemetry  t={MatchTimeSeconds:0.00}s  [{DebugOverlayKey} to hide]", overlayStyle);
        if (!IsAuthoritative)
        {
            GUILayout.Label("Detailed event history is recorded on the server/host only.", overlayStyle);
            GUILayout.EndScrollView();
            GUILayout.EndArea();
            return;
        }

        if (statsByClient.Count == 0)
            GUILayout.Label("No player match stats yet.", overlayStyle);

        foreach (KeyValuePair<ulong, PlayerMatchStats> pair in statsByClient)
            DrawPlayerStats(pair.Value);

        GUILayout.Space(8f);
        GUILayout.Label($"Event history ({events.Count})", overlayStyle);
        int start = Mathf.Max(0, events.Count - 24);
        for (int i = start; i < events.Count; i++)
            GUILayout.Label(events[i].Format(), overlayStyle);

        GUILayout.EndScrollView();
        GUILayout.EndArea();
    }

    private void DrawPlayerStats(PlayerMatchStats stats)
    {
        GUILayout.Label(
            $"Client {stats.ClientId}  Elim:{stats.Eliminations}  Ast:{stats.Assists}  Dth:{stats.Deaths}  " +
            $"Att:{stats.AttachedBullseyeEliminations}  DetElim:{stats.DetachedBullseyeEliminations}  " +
            $"Slam:{stats.BodySlamEliminations}  Detach:{stats.BullseyesDetached}  " +
            $"Shots:{stats.ShotsFired}  Hits:{stats.BullseyeHits}  " +
            $"Hit%:{stats.BullseyeHitPercentage:P0}  AvgDist:{stats.AverageFirearmEliminationDistance:0.0}  " +
            $"Long:{stats.LongestFirearmEliminationDistance:0.0}",
            overlayStyle);

        if (stats.EliminationsByWeapon.Count > 0)
        {
            foreach (KeyValuePair<string, int> weapon in stats.EliminationsByWeapon)
                GUILayout.Label($"    {weapon.Key}: {weapon.Value}", overlayStyle);
        }

        if (stats.FirearmEliminationDistances.Count > 0)
        {
            string distances = "    Distances:";
            for (int i = 0; i < stats.FirearmEliminationDistances.Count; i++)
                distances += $" {stats.FirearmEliminationDistances[i]:0.0}";
            GUILayout.Label(distances, overlayStyle);
        }

        if (stats.DetachmentsByMethod.Count > 0)
        {
            foreach (KeyValuePair<BullseyeDetachMethod, int> method in stats.DetachmentsByMethod)
                GUILayout.Label($"    Detach {method.Key}: {method.Value}", overlayStyle);
        }
    }
}
