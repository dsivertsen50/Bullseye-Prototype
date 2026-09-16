using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Unity.Services.Authentication;
using Unity.Services.Core;
using UnityEngine;

/// <summary>
/// One-shot Unity Gaming Services init plus anonymous prototype auth.
/// Does not replace REQ-062 PlayerProfileId.
/// </summary>
public static class OnlineServicesBootstrap
{
    private static Task<OnlineServicesResult> inFlight;

    public static bool IsInitialized =>
        UnityServices.State == ServicesInitializationState.Initialized;

    public static bool IsAuthenticated =>
        IsInitialized &&
        AuthenticationService.Instance != null &&
        AuthenticationService.Instance.IsSignedIn;

    /// <summary>
    /// Unity Authentication player id. Separate from PlayerProfileId.
    /// </summary>
    public static string UnityPlayerId =>
        IsAuthenticated ? AuthenticationService.Instance.PlayerId : string.Empty;

    public static Task<OnlineServicesResult> EnsureReadyAsync()
    {
        if (IsAuthenticated)
            return Task.FromResult(OnlineServicesResult.Success());

        if (inFlight != null && !inFlight.IsCompleted)
            return inFlight;

        inFlight = EnsureReadyInternalAsync();
        return inFlight;
    }

    private static async Task<OnlineServicesResult> EnsureReadyInternalAsync()
    {
        try
        {
            if (Application.internetReachability == NetworkReachability.NotReachable)
            {
                MultiplayerLog.Info("No Internet reachability reported.");
                return OnlineServicesResult.Fail(
                    "Unable to reach online services.",
                    "Check your Internet connection and try again.");
            }

            if (!IsInitialized)
            {
                MultiplayerLog.Info("Initializing Unity Gaming Services.");
                await UnityServices.InitializeAsync();
                MultiplayerLog.Info("Unity Gaming Services initialized.");
            }

            if (AuthenticationService.Instance == null)
            {
                return OnlineServicesResult.Fail(
                    "Unable to start online services.",
                    "Authentication is unavailable in this build.");
            }

            if (!AuthenticationService.Instance.IsSignedIn)
            {
                SwitchToProcessProfile();
                MultiplayerLog.Info("Authenticating anonymously.");
                await AuthenticationService.Instance.SignInAnonymouslyAsync();
            }

            MultiplayerLog.Info("Authentication succeeded. UnityPlayerId: " + UnityPlayerId);
            return OnlineServicesResult.Success();
        }
        catch (Exception exception)
        {
            inFlight = null;
            MultiplayerLog.Error("Online services initialization failed.", exception);
            return OnlineServicesResult.Fail(
                "Unable to start online services.",
                "Check your Internet connection and Unity project setup.");
        }
    }

    private static void SwitchToProcessProfile()
    {
        try
        {
            string profile = "bullseye_" + Process.GetCurrentProcess().Id;
            AuthenticationService.Instance.SwitchProfile(profile);
        }
        catch (Exception exception)
        {
            MultiplayerLog.Error("Unable to switch authentication profile.", exception);
        }
    }
}

public readonly struct OnlineServicesResult
{
    public readonly bool Succeeded;
    public readonly string PlayerMessage;
    public readonly string DetailMessage;

    private OnlineServicesResult(bool succeeded, string playerMessage, string detailMessage)
    {
        Succeeded = succeeded;
        PlayerMessage = playerMessage;
        DetailMessage = detailMessage;
    }

    public static OnlineServicesResult Success()
    {
        return new OnlineServicesResult(true, null, null);
    }

    public static OnlineServicesResult Fail(string playerMessage, string detailMessage)
    {
        return new OnlineServicesResult(false, playerMessage, detailMessage);
    }
}
