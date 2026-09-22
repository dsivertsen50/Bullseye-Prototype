using System;
using Unity.Services.Multiplayer;

/// <summary>
/// Maps Unity session/service exceptions to short player-facing copy.
/// Technical details stay in the console.
/// </summary>
public static class OnlineSessionErrorMapper
{
    public static string ToPlayerMessage(Exception exception)
    {
        if (exception == null)
            return "Something went wrong. Try again.";

        Exception inner = exception;
        while (inner.InnerException != null)
            inner = inner.InnerException;

        if (inner is SessionException sessionException)
            return FromSessionError(sessionException.Error, sessionException.Message);

        string message = inner.Message ?? string.Empty;
        if (Contains(message, "already started") || Contains(message, "in progress") || Contains(message, "locked"))
            return "This match has already started.";
        if (Contains(message, "not found") || Contains(message, "does not exist") || Contains(message, "invalid code") || Contains(message, "no longer"))
            return "Unable to join lobby.\nCheck the join code and try again.";
        if (Contains(message, "full"))
            return "This lobby is full.";
        if (Contains(message, "timeout") || Contains(message, "timed out"))
            return "Unable to connect.\nPlease check your connection and try again.";
        if (Contains(message, "network") || Contains(message, "internet") || Contains(message, "offline"))
            return "Unable to connect.\nPlease check your connection and try again.";
        if (Contains(message, "auth"))
            return "Unable to sign in to online services.\nCheck your Internet connection and try again.";

        return "Unable to join lobby.\nCheck the join code and try again.";
    }

    public static string FromSessionError(SessionError error, string technicalMessage = null)
    {
        switch (error)
        {
            case SessionError.SessionNotFound:
            case SessionError.SessionDeleted:
                return "This lobby is no longer available.";
            case SessionError.InvalidSessionIdentifier:
            case SessionError.InvalidParameter:
                return "Unable to join lobby.\nCheck the join code and try again.";
            case SessionError.NotAuthorized:
                return "Unable to sign in to online services.\nCheck your Internet connection and try again.";
            case SessionError.RateLimitExceeded:
                return "Too many attempts. Wait a moment and try again.";
            case SessionError.Forbidden:
                return "This match has already started.";
            case SessionError.NetworkSetupFailed:
            case SessionError.NetworkManagerStartFailed:
            case SessionError.NetworkManagerNotInitialized:
            case SessionError.InvalidNetworkConfig:
            case SessionError.TransportComponentMissing:
                return "Unable to start the online connection.\nTry again from the menu.";
            case SessionError.AllocationNotFound:
            case SessionError.AllocationAlreadyExists:
                return "Unable to create an online match.\nTry again.";
            case SessionError.None:
            case SessionError.Unknown:
            default:
                if (!string.IsNullOrEmpty(technicalMessage) &&
                    technicalMessage.IndexOf("full", StringComparison.OrdinalIgnoreCase) >= 0)
                    return "This lobby is full.";
                return "Unable to join lobby.\nCheck the join code and try again.";
        }
    }

    private static bool Contains(string message, string token)
    {
        return !string.IsNullOrEmpty(message) &&
               message.IndexOf(token, StringComparison.OrdinalIgnoreCase) >= 0;
    }
}
