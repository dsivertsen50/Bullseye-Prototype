using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

/// <summary>
/// In-memory and temp-directory checks for REQ-062 persistence and aggregation.
/// Does not touch the live player profile unless explicitly asked.
/// </summary>
public static class PlayerProfileSelfTest
{
    public static bool RunAll(out string report)
    {
        var lines = new List<string>();
        int passed = 0;
        int failed = 0;

        Run("A/B First launch + restart persistence", TestPersistenceAcrossReload, lines, ref passed, ref failed);
        Run("C Match aggregation", TestMatchAggregation, lines, ref passed, ref failed);
        Run("D Second match accumulates", TestSecondMatchAccumulates, lines, ref passed, ref failed);
        Run("E Weapon statistics stay independent", TestWeaponIsolation, lines, ref passed, ref failed);
        Run("E2 Weapon stats survive JSON reload", TestWeaponStatsSerialize, lines, ref passed, ref failed);
        Run("F Longest elimination uses MAX", TestLongestIsMax, lines, ref passed, ref failed);
        Run("G Corrupt save falls back", TestCorruptSave, lines, ref passed, ref failed);
        Run("H Profile reset generates a new id", TestReset, lines, ref passed, ref failed);
        Run("Derived metrics are calculated", TestDerivedMetrics, lines, ref passed, ref failed);
        Run("Identity is not a network id", TestIdentitySeparation, lines, ref passed, ref failed);
        Run("Career screen formatting", TestCareerFormatting, lines, ref passed, ref failed);
        Run("Career empty / zero-death / zero-shot display", TestCareerEmptyStates, lines, ref passed, ref failed);
        Run("Career weapon sort and favorite", TestCareerWeapons, lines, ref passed, ref failed);

        report = $"Player profile self-test: {passed} passed, {failed} failed\n" + string.Join("\n", lines);
        if (failed == 0)
            Debug.Log($"{PlayerProfileConstants.LogPrefix} {report}");
        else
            Debug.LogError($"{PlayerProfileConstants.LogPrefix} {report}");

        return failed == 0;
    }

    private static void Run(
        string name,
        Func<string> test,
        List<string> lines,
        ref int passed,
        ref int failed)
    {
        try
        {
            string error = test();
            if (string.IsNullOrEmpty(error))
            {
                passed++;
                lines.Add("PASS  " + name);
            }
            else
            {
                failed++;
                lines.Add("FAIL  " + name + " — " + error);
            }
        }
        catch (Exception exception)
        {
            failed++;
            lines.Add("FAIL  " + name + " — " + exception.Message);
        }
    }

    private static string TestPersistenceAcrossReload()
    {
        using TempProfileStore store = TempProfileStore.Create();
        PlayerProfile created = store.Persistence.LoadOrCreate(out ProfileLoadResult createResult);
        if (createResult != ProfileLoadResult.Created)
            return "Expected a new profile on first launch.";
        if (string.IsNullOrWhiteSpace(created.PlayerProfileId))
            return "New profile missing PlayerProfileId.";
        if (created.SchemaVersion != PlayerProfileConstants.CurrentSchemaVersion)
            return "New profile missing schema version.";

        store.Persistence.Save(created);
        string id = created.PlayerProfileId;

        PlayerProfile loaded = store.Persistence.LoadOrCreate(out ProfileLoadResult loadResult);
        if (loadResult != ProfileLoadResult.Loaded)
            return "Expected the saved profile to reload.";
        if (loaded.PlayerProfileId != id)
            return $"Id changed across reload ({id} -> {loaded.PlayerProfileId}).";

        return null;
    }

    private static string TestMatchAggregation()
    {
        var profile = PlayerProfile.CreateDefault();
        profile.ApplyMatch(CreateSampleMatch(10, 6, 3, 100, 40));
        LifetimeStats life = profile.LifetimeStats;
        if (life.Eliminations != 10) return $"Eliminations {life.Eliminations}";
        if (life.Deaths != 6) return $"Deaths {life.Deaths}";
        if (life.Assists != 3) return $"Assists {life.Assists}";
        if (life.ShotsFired != 100) return $"ShotsFired {life.ShotsFired}";
        if (life.ShotsHit != 40) return $"ShotsHit {life.ShotsHit}";
        if (life.MatchesPlayed != 1) return $"MatchesPlayed {life.MatchesPlayed}";
        return null;
    }

    private static string TestSecondMatchAccumulates()
    {
        var profile = PlayerProfile.CreateDefault();
        profile.ApplyMatch(CreateSampleMatch(10, 6, 3, 100, 40));
        profile.ApplyMatch(CreateSampleMatch(4, 2, 1, 20, 8));
        LifetimeStats life = profile.LifetimeStats;
        if (life.Eliminations != 14) return $"Eliminations {life.Eliminations}";
        if (life.Deaths != 8) return $"Deaths {life.Deaths}";
        if (life.Assists != 4) return $"Assists {life.Assists}";
        if (life.ShotsFired != 120) return $"ShotsFired {life.ShotsFired}";
        if (life.ShotsHit != 48) return $"ShotsHit {life.ShotsHit}";
        if (life.MatchesPlayed != 2) return $"MatchesPlayed {life.MatchesPlayed}";
        return null;
    }

    private static string TestWeaponIsolation()
    {
        var profile = PlayerProfile.CreateDefault();
        var match = new FinalizedMatchStats { Completed = true };
        match.GetOrCreateWeapon("pistol").Eliminations = 2;
        match.GetOrCreateWeapon("dmr").Eliminations = 5;
        match.GetOrCreateWeapon("shotgun").Eliminations = 3;
        match.Eliminations = 10;
        profile.ApplyMatch(match);

        if (profile.GetWeaponStats("pistol")?.Eliminations != 2) return "pistol";
        if (profile.GetWeaponStats("dmr")?.Eliminations != 5) return "dmr";
        if (profile.GetWeaponStats("shotgun")?.Eliminations != 3) return "shotgun";
        if (profile.FavoriteWeaponId != "dmr") return "favorite";
        return null;
    }

    private static string TestWeaponStatsSerialize()
    {
        using TempProfileStore store = TempProfileStore.Create();
        PlayerProfile profile = store.Persistence.LoadOrCreate(out _);
        profile.GetOrCreateWeaponStats("pistol").Eliminations = 2;
        profile.GetOrCreateWeaponStats("dmr").Eliminations = 5;
        store.Persistence.Save(profile);

        PlayerProfile loaded = store.Persistence.LoadOrCreate(out ProfileLoadResult result);
        if (result != ProfileLoadResult.Loaded)
            return "Reload failed.";
        if (loaded.GetWeaponStats("pistol")?.Eliminations != 2)
            return "pistol did not persist.";
        if (loaded.GetWeaponStats("dmr")?.Eliminations != 5)
            return "dmr did not persist.";
        return null;
    }

    private static string TestLongestIsMax()
    {
        var profile = PlayerProfile.CreateDefault();
        profile.ApplyMatch(new FinalizedMatchStats
        {
            Completed = true,
            LongestEliminationDistance = 42f,
            TotalEliminationDistance = 42f,
            EliminationDistanceSamples = 1
        });
        profile.ApplyMatch(new FinalizedMatchStats
        {
            Completed = true,
            LongestEliminationDistance = 30f,
            TotalEliminationDistance = 30f,
            EliminationDistanceSamples = 1
        });
        if (!Approximately(profile.LifetimeStats.LongestEliminationDistance, 42f))
            return $"After 30m match: {profile.LifetimeStats.LongestEliminationDistance}";

        profile.ApplyMatch(new FinalizedMatchStats
        {
            Completed = true,
            LongestEliminationDistance = 75f,
            TotalEliminationDistance = 75f,
            EliminationDistanceSamples = 1
        });
        if (!Approximately(profile.LifetimeStats.LongestEliminationDistance, 75f))
            return $"After 75m match: {profile.LifetimeStats.LongestEliminationDistance}";

        return null;
    }

    private static string TestCorruptSave()
    {
        using TempProfileStore store = TempProfileStore.Create();
        PlayerProfile created = store.Persistence.LoadOrCreate(out _);
        store.Persistence.Save(created);
        File.WriteAllText(store.Persistence.FilePath, "{ this is not json");

        PlayerProfile fallback = store.Persistence.LoadOrCreate(out ProfileLoadResult result);
        if (result != ProfileLoadResult.ReplacedCorrupt && result != ProfileLoadResult.RecoveredFromBackup)
            return $"Unexpected load result {result}";
        if (fallback == null || string.IsNullOrWhiteSpace(fallback.PlayerProfileId))
            return "Fallback profile was not created.";
        if (Directory.GetFiles(store.Directory, "*.corrupt.*.json").Length == 0)
            return "Corrupt file was not preserved.";
        return null;
    }

    private static string TestReset()
    {
        using TempProfileStore store = TempProfileStore.Create();
        PlayerProfile created = store.Persistence.LoadOrCreate(out _);
        created.LifetimeStats.Eliminations = 12;
        store.Persistence.Save(created);
        string oldId = created.PlayerProfileId;

        store.Persistence.DeleteFiles();
        PlayerProfile reset = store.Persistence.LoadOrCreate(out ProfileLoadResult result);
        if (result != ProfileLoadResult.Created)
            return "Reset did not create a new profile.";
        if (reset.PlayerProfileId == oldId)
            return "Reset reused the old PlayerProfileId.";
        if (reset.LifetimeStats.Eliminations != 0)
            return "Reset did not clear stats.";
        return null;
    }

    private static string TestDerivedMetrics()
    {
        var stats = new LifetimeStats
        {
            Eliminations = 12,
            Deaths = 4,
            ShotsFired = 100,
            ShotsHit = 40,
            MatchesCompleted = 5,
            Wins = 2
        };

        if (!Approximately(stats.KDRatio, 3f)) return "KDRatio stored or wrong.";
        if (!Approximately(stats.Accuracy, 0.4f)) return "Accuracy stored or wrong.";
        if (!Approximately(stats.WinRate, 0.4f)) return "WinRate stored or wrong.";
        return null;
    }

    private static string TestIdentitySeparation()
    {
        var profile = PlayerProfile.CreateDefault();
        if (profile.PlayerProfileId == "0")
            return "PlayerProfileId looks like a default ClientId.";
        if (!Guid.TryParse(profile.PlayerProfileId, out _))
            return "PlayerProfileId is not a GUID.";
        if (!string.IsNullOrEmpty(profile.Identity.SteamId))
            return "SteamId should start unused.";

        var match = new FinalizedMatchStats { NetworkClientId = 7, Eliminations = 1, Completed = true };
        profile.ApplyMatch(match);
        if (profile.PlayerProfileId == "7")
            return "FinalizeMatch replaced PlayerProfileId with NetworkClientId.";
        return null;
    }

    private static string TestCareerFormatting()
    {
        if (ProfileStatFormatter.Count(1204) != "1,204")
            return "Count 1204";
        if (ProfileStatFormatter.Count(123456) != "123,456")
            return "Count 123456";
        if (ProfileStatFormatter.Percent(0.34157f) != "34.2%")
            return "Percent";
        if (ProfileStatFormatter.Ratio(1.28791f) != "1.29")
            return "Ratio";
        if (ProfileStatFormatter.DistanceMeters(91.4283f) != "91.4 m")
            return "Distance";
        if (ProfileStatFormatter.PlayTime(15432f) != "4h 17m")
            return "PlayTime 15432";
        if (ProfileStatFormatter.PlayTime(2820f) != "47m")
            return "PlayTime 47m";
        if (ProfileStatFormatter.ShortProfileId("4fa7d893-aaaa-bbbb-cccc-ddddeeeeffff") != "4fa7d893...")
            return "ShortProfileId";

        var stats = new LifetimeStats
        {
            MatchesPlayed = 20,
            MatchesCompleted = 20,
            Wins = 8,
            Losses = 12,
            Eliminations = 210,
            Deaths = 150,
            Assists = 67,
            ShotsFired = 2000,
            ShotsHit = 700
        };
        if (ProfileStatFormatter.Ratio(stats.KDRatio) != "1.40")
            return "K/D presentation";
        if (ProfileStatFormatter.Percent(stats.Accuracy) != "35.0%")
            return "Accuracy presentation";
        if (ProfileStatFormatter.Percent(stats.WinRate) != "40.0%")
            return "Win rate presentation";
        return null;
    }

    private static string TestCareerEmptyStates()
    {
        var empty = new LifetimeStats();
        if (ProfileStatFormatter.Ratio(empty.KDRatio) != "0.00")
            return "Empty K/D";
        if (ProfileStatFormatter.Percent(empty.Accuracy) != "0.0%")
            return "Empty accuracy";
        if (ProfileStatFormatter.Percent(empty.WinRate) != "0.0%")
            return "Empty win rate";
        if (ProfileStatFormatter.DistanceMeters(empty.LongestEliminationDistance) != ProfileStatFormatter.EmDash)
            return "Empty longest";
        if (ProfileStatFormatter.PlayTime(empty.TotalPlayTimeSeconds) != "0m")
            return "Empty play time";
        if (WeaponDisplayNames.FavoriteWeapon(PlayerProfile.CreateDefault()) != ProfileStatFormatter.EmDash)
            return "Empty favorite";

        var zeroDeaths = new LifetimeStats { Eliminations = 5, Deaths = 0 };
        if (float.IsInfinity(zeroDeaths.KDRatio) || float.IsNaN(zeroDeaths.KDRatio))
            return "Zero-death K/D was Infinity/NaN";
        if (ProfileStatFormatter.Ratio(zeroDeaths.KDRatio) != "5.00")
            return "Zero-death K/D display";

        var zeroShots = new LifetimeStats { ShotsFired = 0, ShotsHit = 0 };
        if (float.IsNaN(zeroShots.Accuracy))
            return "Zero-shot accuracy was NaN";
        if (ProfileStatFormatter.Percent(zeroShots.Accuracy) != "0.0%")
            return "Zero-shot accuracy display";

        if (ProfileStatFormatter.Ratio(float.PositiveInfinity) != "0.00")
            return "Infinity ratio guard";
        if (ProfileStatFormatter.Percent(float.NaN) != "0.0%")
            return "NaN percent guard";
        return null;
    }

    private static string TestCareerWeapons()
    {
        var profile = PlayerProfile.CreateDefault();
        profile.GetOrCreateWeaponStats("pistol").Eliminations = 10;
        profile.GetOrCreateWeaponStats("dmr").Eliminations = 50;
        profile.GetOrCreateWeaponStats("ak").Eliminations = 40;
        profile.GetOrCreateWeaponStats("unused").Eliminations = 0;

        if (profile.FavoriteWeaponId != "dmr")
            return "Favorite should be dmr";
        if (WeaponDisplayNames.Get("dmr") == "weapon_dmr")
            return "Display name leaked internal id";

        System.Collections.Generic.List<WeaponLifetimeStats> sorted =
            PlayerProfilePresentation.GetSortedWeaponStats(profile);
        if (sorted.Count != 3)
            return "Unused weapon should be hidden";
        if (sorted[0].WeaponId != "dmr" || sorted[1].WeaponId != "ak" || sorted[2].WeaponId != "pistol")
            return "Weapons were not sorted by eliminations";
        return null;
    }

    private static FinalizedMatchStats CreateSampleMatch(
        int eliminations,
        int deaths,
        int assists,
        int shotsFired,
        int shotsHit)
    {
        return new FinalizedMatchStats
        {
            Completed = true,
            Eliminations = eliminations,
            Deaths = deaths,
            Assists = assists,
            ShotsFired = shotsFired,
            ShotsHit = shotsHit
        };
    }

    private static bool Approximately(float a, float b)
    {
        return Math.Abs(a - b) < 0.0001f;
    }

    private sealed class TempProfileStore : IDisposable
    {
        public string Directory { get; }
        public PlayerProfilePersistence Persistence { get; }

        private TempProfileStore(string directory, PlayerProfilePersistence persistence)
        {
            Directory = directory;
            Persistence = persistence;
        }

        public static TempProfileStore Create()
        {
            string directory = Path.Combine(Path.GetTempPath(), "BullseyeProfileSelfTest", Guid.NewGuid().ToString("N"));
            return new TempProfileStore(directory, new PlayerProfilePersistence(directory));
        }

        public void Dispose()
        {
            try
            {
                if (System.IO.Directory.Exists(Directory))
                    System.IO.Directory.Delete(Directory, true);
            }
            catch
            {
                // Temp cleanup is best-effort.
            }
        }
    }
}
