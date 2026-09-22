using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;
using Comfort.Common;
using EFT;
using MoreBotsAPI.Components;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using SPT.Common.Http;
using UnityEngine;

namespace ArmyOfTwo.Client;

internal static class VendettaClientBridge
{
    private static readonly FieldInfo HuntWaitTime = typeof(BotHuntManager).GetField(
        "waitTime",
        BindingFlags.Instance | BindingFlags.NonPublic);
    private static readonly HashSet<string> LoggedAssignments = new HashSet<string>(StringComparer.Ordinal);

    private static int _worldId;
    private static bool _statusLoaded;
    private static bool _directHuntActive;

    internal static bool DirectHuntActive
    {
        get
        {
            EnsureStatusLoaded();
            return _directHuntActive;
        }
    }

    internal static void Reset()
    {
        _worldId = 0;
        _statusLoaded = false;
        _directHuntActive = false;
        LoggedAssignments.Clear();
    }

    internal static void AssignLocalPlayer(HuntManager huntManager, BotHuntManager hunter)
    {
        if (hunter?.botOwner == null)
        {
            return;
        }

        var targetLock = hunter.botOwner.GetComponent<VendettaHuntTargetLock>();
        if (targetLock == null)
        {
            targetLock = hunter.botOwner.gameObject.AddComponent<VendettaHuntTargetLock>();
        }
        targetLock.Init(huntManager, hunter);

        if (!DirectHuntActive)
        {
            return;
        }

        MaintainLocalPlayerTarget(huntManager, hunter);

        if (LoggedAssignments.Add(hunter.botOwner.ProfileId))
        {
            Plugin.Log?.LogInfo(
                $"Vendetta hunt: {hunter.botOwner.Profile?.Info?.Nickname ?? "Army of Two bot"} " +
                "received the local player as the direct hunt target.");
        }
    }

    internal static void MaintainLocalPlayerTarget(HuntManager huntManager, BotHuntManager hunter)
    {
        if (hunter?.botOwner == null || hunter.botOwner.IsDead ||
            huntManager == null || !Singleton<GameWorld>.Instantiated)
        {
            return;
        }

        var player = Singleton<GameWorld>.Instance.MainPlayer;
        if (player == null || player.IsAI || !player.HealthController.IsAlive)
        {
            return;
        }

        hunter.priorityTargets ??= new List<IPlayer>();
        hunter.priorityTargets.Clear();
        hunter.priorityTargets.Add(player);
        hunter.huntTarget = player;
        hunter.knownLocation = player.Position;
        hunter.active = true;
        hunter.ignoreRegroup = true;
        hunter.shouldRegroup = false;
        hunter.isRegrouping = false;
        hunter.shouldSearch = false;

        var group = hunter.botOwner.BotsGroup;
        if (group != null)
        {
            huntManager.huntGroups[group] = player;
        }

        HuntWaitTime?.SetValue(hunter, Time.time);
    }

    internal static void ReportEvent(
        string boss,
        string eventName,
        bool killedByPlayer = false,
        bool waitForServer = false)
    {
        if (string.IsNullOrWhiteSpace(RequestHandler.SessionId))
        {
            return;
        }

        if (waitForServer)
        {
            SendEvent(boss, eventName, killedByPlayer);
            return;
        }

        _ = SendEventAsync(boss, eventName, killedByPlayer);
    }

    private static void EnsureStatusLoaded()
    {
        if (!Singleton<GameWorld>.Instantiated)
        {
            return;
        }

        var worldId = Singleton<GameWorld>.Instance.GetInstanceID();
        if (_worldId != worldId)
        {
            _worldId = worldId;
            _statusLoaded = false;
            _directHuntActive = false;
            LoggedAssignments.Clear();
        }

        if (_statusLoaded)
        {
            return;
        }

        _statusLoaded = true;
        try
        {
            var json = RequestHandler.GetJson("/army-of-two/vendetta/status");
            var token = JToken.Parse(json);
            if (token["data"] is JObject wrappedData)
            {
                token = wrappedData;
            }

            _directHuntActive = token.Value<bool?>("directHuntActive")
                ?? token.Value<bool?>("huntActive")
                ?? false;
            if (_directHuntActive)
            {
                Plugin.Log?.LogInfo("Army of Two Hunting Mode is active for this raid.");
            }
        }
        catch (Exception exception)
        {
            _statusLoaded = false;
            _directHuntActive = false;
            Plugin.Log?.LogWarning($"Could not read Army of Two vendetta status: {exception.Message}");
        }
    }

    private static void SendEvent(string boss, string eventName, bool killedByPlayer)
    {
        try
        {
            var payload = JsonConvert.SerializeObject(new
            {
                boss,
                @event = eventName,
                killedByPlayer
            });
            RequestHandler.PostJson("/army-of-two/vendetta/event", payload);
        }
        catch (Exception exception)
        {
            Plugin.Log?.LogWarning(
                $"Could not report {boss} vendetta event '{eventName}': {exception.Message}");
        }
    }

    private static async Task SendEventAsync(string boss, string eventName, bool killedByPlayer)
    {
        try
        {
            var payload = JsonConvert.SerializeObject(new
            {
                boss,
                @event = eventName,
                killedByPlayer
            });
            await RequestHandler
                .PostJsonAsync("/army-of-two/vendetta/event", payload)
                .ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            Plugin.Log?.LogWarning(
                $"Could not report {boss} vendetta event '{eventName}': {exception.Message}");
        }
    }
}
