using System;
using UnityEngine;

/// <summary>
/// Diagnostic logs for online/local session setup. Never logs tokens or secrets.
/// </summary>
public static class MultiplayerLog
{
    public const string Prefix = "[Multiplayer] ";

    public static void Info(string message)
    {
        Debug.Log(Prefix + message);
    }

    public static void Relay(string message)
    {
        Debug.Log("[Relay] " + message);
    }

    public static void Lobby(string message)
    {
        Debug.Log("[Lobby] " + message);
    }

    public static void Warning(string message)
    {
        Debug.LogWarning(Prefix + message);
    }

    public static void Error(string message, Exception exception = null)
    {
        if (exception == null)
        {
            Debug.LogError(Prefix + message);
            return;
        }

        Debug.LogError(Prefix + message + " " + exception.GetType().Name + ": " + exception.Message);
    }
}
