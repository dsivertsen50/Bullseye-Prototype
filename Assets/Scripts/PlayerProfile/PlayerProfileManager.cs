using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Session-long local profile service. Gameplay systems should call this
/// rather than writing profile files themselves.
/// </summary>
[DefaultExecutionOrder(-300)]
public class PlayerProfileManager : MonoBehaviour
{
    public const Key DebugOverlayKey = Key.F10;

    private static PlayerProfileManager instance;

    [SerializeField] private bool showDebugOverlay;
    [SerializeField] private string editorProfileSlot;

    private PlayerProfilePersistence persistence;
    private PlayerProfile profile;
    private bool overlayKeyWasDown;
    private GUIStyle overlayStyle;
    private Vector2 overlayScroll;

    public static PlayerProfileManager Instance => instance;

    public PlayerProfile Profile => profile;
    public string PlayerProfileId => profile != null ? profile.PlayerProfileId : "";
    public string DisplayName => profile != null ? profile.DisplayName : PlayerProfileConstants.DefaultDisplayName;
    public string SavePath => persistence != null ? persistence.FilePath : "";
    public string ActiveSlot { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        instance = null;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap()
    {
        Ensure();
    }

    public static PlayerProfileManager Ensure()
    {
        if (instance != null)
            return instance;

        instance = FindAnyObjectByType<PlayerProfileManager>();
        if (instance != null)
        {
            instance.EnsureLoaded();
            return instance;
        }

        var go = new GameObject("PlayerProfileManager");
        if (Application.isPlaying)
            DontDestroyOnLoad(go);

        instance = go.AddComponent<PlayerProfileManager>();
        instance.EnsureLoaded();
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

        if (GetComponent<PlayerProfileMatchBridge>() == null)
            gameObject.AddComponent<PlayerProfileMatchBridge>();

        EnsureLoaded();
    }

    private void OnDestroy()
    {
        if (instance == this)
            instance = null;
    }

    public PlayerProfile GetProfile()
    {
        EnsureLoaded();
        return profile;
    }

    public void SetDisplayName(string displayName)
    {
        EnsureLoaded();
        profile.Identity.SetDisplayName(displayName);
        SaveProfile();
    }

    public void FinalizeMatch(FinalizedMatchStats stats)
    {
        if (stats == null)
        {
            Debug.LogWarning($"{PlayerProfileConstants.LogPrefix} FinalizeMatch ignored a null snapshot.");
            return;
        }

        EnsureLoaded();
        profile.ApplyMatch(stats);
        SaveProfile();
        Debug.Log(
            $"{PlayerProfileConstants.LogPrefix} Finalized match:\n" +
            $"Eliminations +{stats.Eliminations}\n" +
            $"Deaths +{stats.Deaths}\n" +
            $"Assists +{stats.Assists}\n" +
            $"ShotsFired +{stats.ShotsFired}\n" +
            $"ShotsHit +{stats.ShotsHit}");
    }

    public void SaveProfile()
    {
        EnsureLoaded();
        persistence.Save(profile);
        Debug.Log($"{PlayerProfileConstants.LogPrefix} Saved profile.");
    }

    public void ReloadFromDisk()
    {
        persistence = CreatePersistence();
        LoadOrCreateInternal();
    }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
    [ContextMenu("Dump Player Profile")]
    public void DumpToLog()
    {
        EnsureLoaded();
        Debug.Log(FormatDebugDump());
    }

    [ContextMenu("Reset Player Profile")]
    public void ResetProfile()
    {
        ResetProfileInternal();
    }
#endif

    public void ResetProfileInternal()
    {
        EnsurePersistence();
        persistence.DeleteFiles();
        profile = PlayerProfile.CreateDefault();
        persistence.Save(profile);
        Debug.Log($"{PlayerProfileConstants.LogPrefix} Created new profile: {profile.PlayerProfileId}");
        Debug.Log($"{PlayerProfileConstants.LogPrefix} Saved profile.");
    }

    private void EnsureLoaded()
    {
        EnsurePersistence();
        if (profile != null)
            return;

        LoadOrCreateInternal();
    }

    private void EnsurePersistence()
    {
        if (persistence != null)
            return;

        persistence = CreatePersistence();
    }

    private PlayerProfilePersistence CreatePersistence()
    {
        ActiveSlot = ResolveSlot();
        return PlayerProfilePersistence.ForLocalInstall(ActiveSlot);
    }

    private string ResolveSlot()
    {
        string slot = PlayerProfilePersistence.ResolveSlotFromEnvironment();
        if (!string.IsNullOrWhiteSpace(slot))
            return slot;

#if UNITY_EDITOR
        if (!string.IsNullOrWhiteSpace(editorProfileSlot))
            return editorProfileSlot;
#endif
        return null;
    }

    private void LoadOrCreateInternal()
    {
        profile = persistence.LoadOrCreate(out ProfileLoadResult result);
        profile.EnsureCollections();

        switch (result)
        {
            case ProfileLoadResult.Created:
            case ProfileLoadResult.ReplacedCorrupt:
                persistence.Save(profile);
                Debug.Log($"{PlayerProfileConstants.LogPrefix} Created new profile: {profile.PlayerProfileId}");
                Debug.Log($"{PlayerProfileConstants.LogPrefix} Saved profile.");
                break;
            case ProfileLoadResult.RecoveredFromBackup:
                persistence.Save(profile);
                Debug.Log($"{PlayerProfileConstants.LogPrefix} Loaded profile: {profile.PlayerProfileId}");
                Debug.Log($"{PlayerProfileConstants.LogPrefix} Saved profile.");
                break;
            default:
                Debug.Log($"{PlayerProfileConstants.LogPrefix} Loaded profile: {profile.PlayerProfileId}");
                break;
        }
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
        const float height = 360f;
        var window = new Rect(24f, 24f, width, height);
        GUI.Box(window, GUIContent.none);
        GUILayout.BeginArea(new Rect(window.x + 8f, window.y + 8f, width - 16f, height - 16f));
        overlayScroll = GUILayout.BeginScrollView(overlayScroll);
        GUILayout.Label($"Player Profile  [{DebugOverlayKey} to hide]", overlayStyle);
        GUILayout.Label(FormatDebugDump(), overlayStyle);
        GUILayout.EndScrollView();
        GUILayout.EndArea();
    }

    public string FormatDebugDump()
    {
        EnsureLoaded();
        LifetimeStats life = profile.LifetimeStats;
        var builder = new System.Text.StringBuilder(512);
        builder.AppendLine($"PlayerProfileId: {profile.PlayerProfileId}");
        builder.AppendLine($"DisplayName: {profile.DisplayName}");
        builder.AppendLine($"SteamId: {(string.IsNullOrEmpty(profile.Identity.SteamId) ? "(unused)" : profile.Identity.SteamId)}");
        builder.AppendLine($"SchemaVersion: {profile.SchemaVersion}");
        builder.AppendLine($"Slot: {(string.IsNullOrEmpty(ActiveSlot) ? "(default)" : ActiveSlot)}");
        builder.AppendLine($"Path: {SavePath}");
        builder.AppendLine($"MatchesPlayed: {life.MatchesPlayed}");
        builder.AppendLine($"MatchesCompleted: {life.MatchesCompleted}");
        builder.AppendLine($"Wins: {life.Wins}  Losses: {life.Losses}");
        builder.AppendLine($"Eliminations: {life.Eliminations}");
        builder.AppendLine($"Deaths: {life.Deaths}");
        builder.AppendLine($"Assists: {life.Assists}");
        builder.AppendLine($"ShotsFired: {life.ShotsFired}  ShotsHit: {life.ShotsHit}");
        builder.AppendLine($"K/D: {life.KDRatio:0.00}  Accuracy: {life.Accuracy:P1}");
        builder.AppendLine($"LongestElim: {life.LongestEliminationDistance:0.0}m");
        builder.AppendLine($"AttachedElims: {life.AttachedBullseyeEliminations}  DetachedElims: {life.DetachedBullseyeEliminations}");
        builder.AppendLine($"BullseyeHits: {life.BullseyeHits}  GrenadeDetach: {life.GrenadeBullseyeDetachments}");
        builder.AppendLine($"BodySlamElims: {life.BodySlamEliminations}");
        if (profile.WeaponStats != null && profile.WeaponStats.Count > 0)
        {
            builder.AppendLine("WeaponStats:");
            for (int i = 0; i < profile.WeaponStats.Count; i++)
            {
                WeaponLifetimeStats weapon = profile.WeaponStats[i];
                if (weapon == null)
                    continue;
                builder.AppendLine(
                    $"  {weapon.WeaponId}: elim {weapon.Eliminations}  shots {weapon.ShotsFired}/{weapon.ShotsHit}  " +
                    $"long {weapon.LongestEliminationDistance:0.0}m");
            }
        }
        else
        {
            builder.AppendLine("WeaponStats: (none)");
        }

        return builder.ToString();
    }
}
