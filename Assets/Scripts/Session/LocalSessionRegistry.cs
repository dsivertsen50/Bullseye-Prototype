using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using UnityEngine;

/// <summary>
/// Prototype session directory for same-machine and local testing.
/// Stores join-code metadata on disk so a second editor/build can find a host
/// without a cloud matchmaking service. This is not online discovery.
/// </summary>
public static class LocalSessionRegistry
{
    private const string DirectoryName = "BullseyePrototype";
    private const string FileName = "local-sessions.json";
    private const string CodeAlphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
    private const int CodeLength = 6;

    [Serializable]
    private class SessionList
    {
        public GameSessionInfo[] sessions = Array.Empty<GameSessionInfo>();
    }

    public static string GenerateJoinCode()
    {
        PruneStale();
        SessionList list = Read();
        for (int attempt = 0; attempt < 32; attempt++)
        {
            char[] chars = new char[CodeLength];
            for (int i = 0; i < CodeLength; i++)
                chars[i] = CodeAlphabet[UnityEngine.Random.Range(0, CodeAlphabet.Length)];

            string code = new string(chars);
            if (Find(list, code) == null)
                return code;
        }

        return DateTime.UtcNow.Ticks.ToString("X").PadLeft(CodeLength, 'A').Substring(0, CodeLength);
    }

    public static void Register(GameSessionInfo info)
    {
        if (info == null || string.IsNullOrEmpty(info.JoinCode))
            return;

        info.JoinCode = NormalizeCode(info.JoinCode);
        info.HostProcessId = Process.GetCurrentProcess().Id;
        if (info.CreatedUtcTicks == 0)
            info.CreatedUtcTicks = DateTime.UtcNow.Ticks;

        List<GameSessionInfo> sessions = new List<GameSessionInfo>(Read().sessions ?? Array.Empty<GameSessionInfo>());
        for (int i = sessions.Count - 1; i >= 0; i--)
        {
            GameSessionInfo existing = sessions[i];
            if (existing == null ||
                existing.HostProcessId == info.HostProcessId ||
                NormalizeCode(existing.JoinCode) == info.JoinCode)
            {
                sessions.RemoveAt(i);
            }
        }

        sessions.Add(info);
        Write(new SessionList { sessions = sessions.ToArray() });
    }

    public static void UnregisterCurrentProcess()
    {
        int pid = Process.GetCurrentProcess().Id;
        List<GameSessionInfo> sessions = new List<GameSessionInfo>(Read().sessions ?? Array.Empty<GameSessionInfo>());
        int removed = sessions.RemoveAll(session => session != null && session.HostProcessId == pid);
        if (removed > 0)
            Write(new SessionList { sessions = sessions.ToArray() });
    }

    public static GameSessionInfo FindByJoinCode(string joinCode)
    {
        PruneStale();
        return Find(Read(), joinCode);
    }

    public static List<GameSessionInfo> ListPublicSessions()
    {
        PruneStale();
        GameSessionInfo[] sessions = Read().sessions ?? Array.Empty<GameSessionInfo>();
        var publicSessions = new List<GameSessionInfo>();
        for (int i = 0; i < sessions.Length; i++)
        {
            GameSessionInfo session = sessions[i];
            if (session != null && session.IsPublic)
                publicSessions.Add(session);
        }

        return publicSessions;
    }

    public static string NormalizeCode(string joinCode)
    {
        if (string.IsNullOrEmpty(joinCode))
            return string.Empty;

        char[] buffer = new char[joinCode.Length];
        int length = 0;
        for (int i = 0; i < joinCode.Length; i++)
        {
            char c = char.ToUpperInvariant(joinCode[i]);
            if (char.IsLetterOrDigit(c))
            {
                buffer[length] = c;
                length++;
            }
        }

        return new string(buffer, 0, length);
    }

    private static GameSessionInfo Find(SessionList list, string joinCode)
    {
        string normalized = NormalizeCode(joinCode);
        if (string.IsNullOrEmpty(normalized) || list == null || list.sessions == null)
            return null;

        for (int i = 0; i < list.sessions.Length; i++)
        {
            GameSessionInfo session = list.sessions[i];
            if (session != null && NormalizeCode(session.JoinCode) == normalized)
                return session;
        }

        return null;
    }

    private static void PruneStale()
    {
        List<GameSessionInfo> sessions = new List<GameSessionInfo>(Read().sessions ?? Array.Empty<GameSessionInfo>());
        int removed = sessions.RemoveAll(IsStale);
        if (removed > 0)
            Write(new SessionList { sessions = sessions.ToArray() });
    }

    private static bool IsStale(GameSessionInfo session)
    {
        if (session == null || session.HostProcessId <= 0)
            return true;

        try
        {
            Process.GetProcessById(session.HostProcessId);
            return false;
        }
        catch (ArgumentException)
        {
            return true;
        }
    }

    private static SessionList Read()
    {
        string path = GetFilePath();
        if (!File.Exists(path))
            return new SessionList();

        try
        {
            string json = File.ReadAllText(path);
            SessionList list = JsonUtility.FromJson<SessionList>(json);
            if (list == null)
                return new SessionList();
            if (list.sessions == null)
                list.sessions = Array.Empty<GameSessionInfo>();
            return list;
        }
        catch (Exception)
        {
            return new SessionList();
        }
    }

    private static void Write(SessionList list)
    {
        string path = GetFilePath();
        string directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            Directory.CreateDirectory(directory);

        string json = JsonUtility.ToJson(list, true);
        string tempPath = path + ".tmp";
        File.WriteAllText(tempPath, json);
        if (File.Exists(path))
            File.Delete(path);
        File.Move(tempPath, path);
    }

    private static string GetFilePath()
    {
        string root = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        if (string.IsNullOrEmpty(root))
            root = Application.persistentDataPath;

        return Path.Combine(root, DirectoryName, FileName);
    }
}
