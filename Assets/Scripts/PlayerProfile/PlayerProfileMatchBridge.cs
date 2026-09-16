using Unity.Netcode;
using UnityEngine;

/// <summary>
/// Session-end adapter: copies local match telemetry into the lifetime profile
/// once when a match stops. Does not write per-shot. The host uses
/// CombatTelemetryManager; clients fall back to replicated PlayerStats.
/// </summary>
public class PlayerProfileMatchBridge : MonoBehaviour
{
    private static PlayerProfileMatchBridge instance;

    private NetworkManager boundManager;
    private bool inMatch;
    private bool finalizedCurrentMatch;
    private FinalizedMatchStats cachedSnapshot;
    private float cacheTimer;

    public static PlayerProfileMatchBridge Instance => instance;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        instance = null;
    }

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(this);
            return;
        }

        instance = this;
    }

    private void OnDestroy()
    {
        UnbindNetwork();
        if (inMatch && !finalizedCurrentMatch)
            TryFinalizeLocalMatch();

        if (instance == this)
            instance = null;
    }

    private void OnApplicationQuit()
    {
        TryFinalizeLocalMatch();
    }

    private void Update()
    {
        BindNetwork();
        if (!inMatch || finalizedCurrentMatch)
            return;

        cacheTimer += Time.unscaledDeltaTime;
        if (cacheTimer < 1f)
            return;

        cacheTimer = 0f;
        CaptureSnapshot();
    }

    public static bool TryFinalizeLocalMatch(bool requireRecordedActivity = false)
    {
        if (instance == null)
        {
            PlayerProfileManager manager = PlayerProfileManager.Ensure();
            instance = manager != null ? manager.GetComponent<PlayerProfileMatchBridge>() : null;
        }

        return instance != null && instance.FinalizeIfNeeded(requireRecordedActivity);
    }

    public void MarkMatchStarted()
    {
        inMatch = true;
        finalizedCurrentMatch = false;
        cachedSnapshot = null;
        cacheTimer = 0f;
    }

    private bool FinalizeIfNeeded(bool requireRecordedActivity)
    {
        if (!inMatch || finalizedCurrentMatch)
            return false;

        FinalizedMatchStats snapshot = FinalizedMatchStatsFactory.FromLocalSession() ?? cachedSnapshot;
        if (snapshot == null)
        {
            if (!requireRecordedActivity)
            {
                finalizedCurrentMatch = true;
                inMatch = false;
            }

            return false;
        }

        if (requireRecordedActivity && !HasActivity(snapshot))
            return false;

        PlayerProfileManager.Ensure().FinalizeMatch(snapshot);
        finalizedCurrentMatch = true;
        inMatch = false;
        cachedSnapshot = null;
        return true;
    }

    private static bool HasActivity(FinalizedMatchStats snapshot)
    {
        if (snapshot.Eliminations > 0 || snapshot.Deaths > 0 || snapshot.Assists > 0)
            return true;
        if (snapshot.ShotsFired > 0 || snapshot.BullseyeHits > 0)
            return true;
        if (snapshot.WeaponStats != null && snapshot.WeaponStats.Count > 0)
            return true;
        return snapshot.PlayTimeSeconds > 1f;
    }

    private void CaptureSnapshot()
    {
        FinalizedMatchStats live = FinalizedMatchStatsFactory.FromLocalSession();
        if (live != null)
            cachedSnapshot = live;
    }

    private void BindNetwork()
    {
        NetworkManager networkManager = NetworkManager.Singleton;
        if (networkManager == boundManager)
        {
            if (networkManager != null && networkManager.IsListening && !inMatch && !finalizedCurrentMatch)
                MarkMatchStarted();
            return;
        }

        UnbindNetwork();
        boundManager = networkManager;
        if (networkManager == null)
            return;

        networkManager.OnServerStarted += HandleMatchStarted;
        networkManager.OnClientStarted += HandleMatchStarted;
        networkManager.OnServerStopped += HandleMatchStopped;
        networkManager.OnClientStopped += HandleMatchStopped;

        if (networkManager.IsListening)
            MarkMatchStarted();
    }

    private void UnbindNetwork()
    {
        if (boundManager == null)
            return;

        boundManager.OnServerStarted -= HandleMatchStarted;
        boundManager.OnClientStarted -= HandleMatchStarted;
        boundManager.OnServerStopped -= HandleMatchStopped;
        boundManager.OnClientStopped -= HandleMatchStopped;
        boundManager = null;
    }

    private void HandleMatchStarted()
    {
        MarkMatchStarted();
    }

    private void HandleMatchStopped(bool _)
    {
        TryFinalizeLocalMatch();
    }
}
