using System;
using System.IO;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Local research telemetry coordinator. Combat outcomes still go through
/// CombatTelemetryManager; this layer records behavioral primitives for
/// later analysis and writes them as separate JSONL datasets.
/// </summary>
public class ResearchTelemetryManager : MonoBehaviour
{
    public const Key DebugOverlayKey = Key.F9;
    public const float FlushIntervalSeconds = 0.35f;

    private static ResearchTelemetryManager instance;

    [Header("Research / Privacy")]
    [SerializeField, Tooltip("When false, rich research telemetry is not collected. Future consent systems should drive this.")]
    private bool researchConsentActive = true;
    [SerializeField] private string experimentId = "";
    [SerializeField] private string experimentalConditionId = "";
    [SerializeField] private int assignmentRandomSeed;
    [SerializeField] private string gameplayConfigVersion = ResearchTelemetryConstants.GameplayConfigVersion;

    [Header("Output")]
    [SerializeField] private bool writeLocalFiles = true;
    [SerializeField] private bool logDroppedSamples;

    [Header("Debug")]
    [SerializeField] private bool showDebugOverlay;
    [SerializeField] private bool logQaSummaries = true;

    private readonly ResearchTelemetryWriter writer = new();
    private ResearchEngagementQaSummary lastSummary;
    private string lastSummaryText = "No completed engagement yet.";
    private string researchParticipantId;
    private string matchId;
    private string outputDirectory;
    private int nextEngagementSerial;
    private int nextShotSerial;
    private int nextAssignmentSerial;
    private double matchStartServerTime;
    private float matchStartRealtime;
    private bool matchClockStarted;
    private bool overlayKeyWasDown;
    private float flushTimer;
    private GUIStyle overlayStyle;
    private Vector2 overlayScroll;

    public static ResearchTelemetryManager Instance => instance;

    public bool ResearchConsentActive => researchConsentActive;
    public string ResearchParticipantId => researchParticipantId;
    public string MatchId => matchId;
    public string ExperimentId => experimentId;
    public string ExperimentalConditionId => experimentalConditionId;
    public string SchemaVersion => ResearchTelemetryConstants.SchemaVersion;
    public string GameVersion => Application.version;
    public string GameplayConfigVersion => gameplayConfigVersion;
    public string OutputDirectory => outputDirectory;
    public ResearchEngagementQaSummary LastCompletedEngagement => lastSummary;
    public string LastEngagementSummaryText => lastSummaryText;
    public int DroppedSampleCount => writer.DroppedCount;

    public float MatchTimeSeconds
    {
        get
        {
            if (NetworkManager.Singleton != null && matchClockStarted)
                return (float)(NetworkManager.Singleton.ServerTime.Time - matchStartServerTime);
            return Mathf.Max(0f, Time.realtimeSinceStartup - matchStartRealtime);
        }
    }

    public float MatchTimeMs => MatchTimeSeconds * 1000f;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        instance = null;
    }

    public static ResearchTelemetryManager Ensure()
    {
        if (instance != null)
            return instance;

        instance = FindAnyObjectByType<ResearchTelemetryManager>();
        if (instance != null)
        {
            instance.BindNetwork();
            return instance;
        }

        var go = new GameObject("ResearchTelemetryManager");
        if (Application.isPlaying)
            DontDestroyOnLoad(go);

        instance = go.AddComponent<ResearchTelemetryManager>();
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

        EnsureParticipantId();
        BindNetwork();
    }

    private void OnEnable()
    {
        BindNetwork();
    }

    private void OnDestroy()
    {
        writer.EndSession();
        UnbindNetwork();
        if (instance == this)
            instance = null;
    }

    private void OnApplicationQuit()
    {
        NotifyMatchEnded();
        writer.Flush();
    }

    private void Update()
    {
        EnsureLocalTracker();
        TickOverlayToggle();

        flushTimer += Time.unscaledDeltaTime;
        if (flushTimer >= FlushIntervalSeconds)
        {
            flushTimer = 0f;
            writer.Flush();
            if (logDroppedSamples && writer.DroppedCount > 0)
                Debug.LogWarning($"Research telemetry dropped {writer.DroppedCount} records to protect gameplay.");
        }
    }

    public void NotifyMatchReset()
    {
        writer.EndSession();
        nextEngagementSerial = 0;
        nextShotSerial = 0;
        nextAssignmentSerial = 0;
        lastSummary = null;
        lastSummaryText = "No completed engagement yet.";
        matchClockStarted = true;
        matchStartRealtime = Time.realtimeSinceStartup;
        matchStartServerTime = NetworkManager.Singleton != null
            ? NetworkManager.Singleton.ServerTime.Time
            : 0d;
        matchId = "M" + Guid.NewGuid().ToString("N")[..10].ToUpperInvariant();
        outputDirectory = null;

        if (writeLocalFiles && researchConsentActive)
        {
            string stamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            outputDirectory = Path.Combine(
                Application.persistentDataPath,
                "ResearchTelemetry",
                $"{stamp}_{matchId}");
            writer.BeginSession(outputDirectory);
        }

        WriteMatchEvent(ResearchTelemetryEventKind.MatchStarted);
        if (!string.IsNullOrWhiteSpace(experimentId) || !string.IsNullOrWhiteSpace(experimentalConditionId))
            RecordExperimentalAssignment(experimentId, experimentalConditionId, assignmentRandomSeed, "MatchStart");
    }

    public void NotifyMatchEnded()
    {
        if (string.IsNullOrEmpty(matchId))
            return;

        WriteMatchEvent(ResearchTelemetryEventKind.MatchEnded);
        writer.Flush();
    }

    public string NextEngagementId()
    {
        nextEngagementSerial++;
        return $"E{nextEngagementSerial:00000}";
    }

    public string NextShotId()
    {
        nextShotSerial++;
        return $"S{nextShotSerial:00000}";
    }

    public string NextAssignmentId()
    {
        nextAssignmentSerial++;
        return $"A{nextAssignmentSerial:00000}";
    }

    public void SetResearchConsent(bool active)
    {
        researchConsentActive = active;
    }

    public void AssignExperimentalCondition(string experiment, string condition, int seed = 0)
    {
        experimentId = experiment ?? "";
        experimentalConditionId = condition ?? "";
        assignmentRandomSeed = seed;
        RecordExperimentalAssignment(experimentId, experimentalConditionId, seed, "RuntimeAssignment");
    }

    public void RecordExperimentalAssignment(string experiment, string condition, int seed, string note)
    {
        if (!CanWrite())
            return;

        var record = new ResearchExperimentalAssignmentRecord
        {
            Header = CreateHeader(ResearchTelemetryEventKind.ExperimentalAssignment),
            ExperimentId = experiment ?? "",
            ExperimentalConditionId = condition ?? "",
            AssignmentRandomSeed = seed,
            AssignmentNote = note ?? ""
        };
        Write("experimental_assignments.jsonl", JsonUtility.ToJson(record));
    }

    public void RecordBullseyeAssignment(
        ulong targetPlayerId,
        ResearchBodyRegion assignedRegion,
        Vector3 assignedLocal,
        Vector3 actualLocal,
        ResearchBullseyeAssignmentReason reason,
        int randomSeed = 0)
    {
        if (!CanWrite())
            return;

        var record = new ResearchBullseyeAssignmentRecord
        {
            Header = CreateHeader(ResearchTelemetryEventKind.BullseyeAssignment),
            BullseyeAssignmentId = NextAssignmentId(),
            TargetPlayerId = targetPlayerId,
            BullseyeAssignmentRandomSeed = randomSeed,
            BullseyeAssignedBodyRegion = assignedRegion.ToString(),
            BullseyeAssignedLocalPosition = ResearchVec3.From(assignedLocal),
            BullseyeActualLocalPosition = ResearchVec3.From(actualLocal),
            BullseyeAssignmentReason = reason.ToString()
        };
        Write("bullseye_assignments.jsonl", JsonUtility.ToJson(record));
    }

    public void WriteEngagementStarted(ResearchEngagementStartedRecord record)
    {
        if (!CanWrite() || record == null)
            return;
        record.Header = CreateHeader(ResearchTelemetryEventKind.EngagementStarted);
        Write("engagements.jsonl", JsonUtility.ToJson(record));
    }

    public void WriteAimSample(ResearchAimSampleRecord record)
    {
        if (!CanWrite() || record == null)
            return;
        record.Header = CreateHeader(ResearchTelemetryEventKind.AimTrajectorySample);
        Write("aim_samples.jsonl", JsonUtility.ToJson(record));
    }

    public void WriteInitialAim(ResearchInitialAimRecord record)
    {
        if (!CanWrite() || record == null)
            return;
        record.Header = CreateHeader(ResearchTelemetryEventKind.InitialAimSnapshot);
        Write("engagements.jsonl", JsonUtility.ToJson(record));
    }

    public void WriteShotFired(ResearchShotFiredRecord record)
    {
        if (!CanWrite() || record == null)
            return;
        record.Header = CreateHeader(ResearchTelemetryEventKind.ShotFired);
        Write("shots.jsonl", JsonUtility.ToJson(record));
    }

    public void WriteShotImpact(ResearchShotImpactRecord record)
    {
        if (!CanWrite() || record == null)
            return;
        record.Header = CreateHeader(ResearchTelemetryEventKind.ShotImpact);
        Write("impacts.jsonl", JsonUtility.ToJson(record));
    }

    public void WriteElimination(ResearchEliminationRecord record)
    {
        if (!CanWrite() || record == null)
            return;
        record.Header = CreateHeader(ResearchTelemetryEventKind.Elimination);
        Write("eliminations.jsonl", JsonUtility.ToJson(record));
    }

    public void WriteEngagementEnded(ResearchEngagementEndedRecord record)
    {
        if (!CanWrite() || record == null)
            return;
        record.Header = CreateHeader(ResearchTelemetryEventKind.EngagementEnded);
        Write("engagements.jsonl", JsonUtility.ToJson(record));
    }

    public void ReportCompletedEngagement(ResearchEngagementQaSummary summary)
    {
        lastSummary = summary;
        lastSummaryText = summary != null ? summary.Format() : "No completed engagement yet.";
        if (logQaSummaries && summary != null)
            Debug.Log("Research telemetry QA\n" + lastSummaryText);
    }

    public ResearchRecordHeader CreateHeader(ResearchTelemetryEventKind kind)
    {
        EnsureMatchClock();
        return new ResearchRecordHeader
        {
            EventKind = kind.ToString(),
            TelemetrySchemaVersion = ResearchTelemetryConstants.SchemaVersion,
            GameVersion = Application.version,
            GameplayConfigVersion = gameplayConfigVersion,
            ResearchParticipantId = researchParticipantId,
            MatchId = matchId,
            Timestamp = Time.realtimeSinceStartup,
            MatchTime = MatchTimeSeconds,
            MatchTimeMs = MatchTimeMs,
            ExperimentId = experimentId,
            ExperimentalConditionId = experimentalConditionId
        };
    }

    private void WriteMatchEvent(ResearchTelemetryEventKind kind)
    {
        if (!CanWrite())
            return;

        var record = new ResearchMatchRecord
        {
            Header = CreateHeader(kind),
            ResearchConsentActive = researchConsentActive,
            InputMethodAtMatchStart = ResearchInputMethod.Unknown.ToString()
        };
        Write("matches.jsonl", JsonUtility.ToJson(record));
    }

    private void Write(string fileName, string json)
    {
        writer.Enqueue(fileName, json);
    }

    private bool CanWrite()
    {
        return researchConsentActive && writeLocalFiles && writer.HasDirectory;
    }

    private void EnsureParticipantId()
    {
        // ResearchParticipantId is not PlayerProfileId. A future consent
        // system should own this value; do not reuse the game profile GUID.
        if (!string.IsNullOrEmpty(researchParticipantId))
            return;

        string stored = PlayerPrefs.GetString(ResearchTelemetryConstants.ParticipantPrefsKey, "");
        if (string.IsNullOrWhiteSpace(stored) || stored.Length < 3 || stored[0] != 'R')
        {
            stored = "R" + Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
            PlayerPrefs.SetString(ResearchTelemetryConstants.ParticipantPrefsKey, stored);
            PlayerPrefs.Save();
        }

        researchParticipantId = stored;
    }

    private void EnsureMatchClock()
    {
        if (matchClockStarted && !string.IsNullOrEmpty(matchId))
            return;

        NotifyMatchReset();
    }

    private void EnsureLocalTracker()
    {
        Camera localCamera = PlayerNetworkSetup.LocalOwnedCamera;
        if (localCamera == null)
            return;

        Transform root = localCamera.GetComponentInParent<NetworkObject>() != null
            ? localCamera.GetComponentInParent<NetworkObject>().transform
            : localCamera.transform.root;
        if (root == null)
            return;

        if (root.GetComponent<ResearchEngagementTracker>() != null)
            return;

        if (root.GetComponent<PlayerHealth>() == null)
            return;

        root.gameObject.AddComponent<ResearchEngagementTracker>();
    }

    private void BindNetwork()
    {
        NetworkManager networkManager = NetworkManager.Singleton;
        if (networkManager == null)
            return;

        networkManager.OnClientStarted -= HandleClientStarted;
        networkManager.OnClientStarted += HandleClientStarted;
        networkManager.OnServerStopped -= HandleSessionStopped;
        networkManager.OnServerStopped += HandleSessionStopped;
        networkManager.OnClientStopped -= HandleSessionStopped;
        networkManager.OnClientStopped += HandleSessionStopped;
    }

    private void UnbindNetwork()
    {
        NetworkManager networkManager = NetworkManager.Singleton;
        if (networkManager == null)
            return;

        networkManager.OnClientStarted -= HandleClientStarted;
        networkManager.OnServerStopped -= HandleSessionStopped;
        networkManager.OnClientStopped -= HandleSessionStopped;
    }

    private void HandleClientStarted()
    {
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsServer)
            return;
        NotifyMatchReset();
    }

    private void HandleSessionStopped(bool _)
    {
        NotifyMatchEnded();
        writer.EndSession();
    }

    private void TickOverlayToggle()
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard == null)
            return;

        bool down = keyboard[DebugOverlayKey].isPressed;
        if (down && !overlayKeyWasDown)
            showDebugOverlay = !showDebugOverlay;
        overlayKeyWasDown = down;
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

        const float width = 460f;
        const float height = 420f;
        var window = new Rect(Screen.width - width - 24f, 136f, width, height);
        GUI.Box(window, GUIContent.none);
        GUILayout.BeginArea(new Rect(window.x + 8f, window.y + 8f, width - 16f, height - 16f));
        overlayScroll = GUILayout.BeginScrollView(overlayScroll);

        GUILayout.Label($"Research Telemetry  t={MatchTimeSeconds:0.00}s  [{DebugOverlayKey} to hide]", overlayStyle);
        GUILayout.Label($"Schema {SchemaVersion}  Game {GameVersion}  Config {gameplayConfigVersion}", overlayStyle);
        GUILayout.Label($"Participant {researchParticipantId}  Match {matchId}", overlayStyle);
        GUILayout.Label($"Consent {(researchConsentActive ? "active" : "off")}  Output {outputDirectory}", overlayStyle);
        GUILayout.Label($"Dropped writes: {writer.DroppedCount}  Pending: {writer.PendingCount}", overlayStyle);
        if (!string.IsNullOrEmpty(experimentId) || !string.IsNullOrEmpty(experimentalConditionId))
            GUILayout.Label($"Experiment {experimentId} / {experimentalConditionId}", overlayStyle);

        ResearchEngagementTracker tracker = FindLocalTracker();
        if (tracker != null)
            GUILayout.Label(tracker.FormatDebugStatus(), overlayStyle);
        else
            GUILayout.Label("No local engagement tracker yet.", overlayStyle);

        GUILayout.Space(8f);
        GUILayout.Label("Last completed engagement", overlayStyle);
        GUILayout.Label(lastSummaryText, overlayStyle);

        GUILayout.EndScrollView();
        GUILayout.EndArea();
    }

    private static ResearchEngagementTracker FindLocalTracker()
    {
        Camera camera = PlayerNetworkSetup.LocalOwnedCamera;
        if (camera == null)
            return null;
        return camera.GetComponentInParent<ResearchEngagementTracker>();
    }
}
