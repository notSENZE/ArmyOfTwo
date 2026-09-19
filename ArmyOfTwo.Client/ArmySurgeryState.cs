using System;
using System.Collections.Generic;
using Comfort.Common;
using EFT;
using UnityEngine;

namespace ArmyOfTwo.Client;

internal sealed class ArmySurgeryState
{
    private const float QuietPeriod = 20f;
    private static readonly Dictionary<string, ArmySurgeryState> States =
        new Dictionary<string, ArmySurgeryState>(StringComparer.Ordinal);
    private static int _worldId;

    private readonly string _botName;
    private float _lastHitTime;
    private float _lastThreatAt;
    private int _attempt;

    private ArmySurgeryState(BotOwner bot)
    {
        _botName = bot.Profile?.Info?.Nickname ?? "Army of Two bot";
        _lastHitTime = bot.Memory?.LastTimeHit ?? 0f;
        _lastThreatAt = Time.time;
    }

    internal bool ActionActive { get; private set; }
    internal bool TreatmentStarted { get; private set; }
    internal bool ActionEnded { get; private set; }
    internal float ActionStartedAt { get; private set; }
    internal float NextEvaluationAt { get; private set; }

    internal static ArmySurgeryState Get(BotOwner bot)
    {
        if (bot == null || !Singleton<GameWorld>.Instantiated)
        {
            return null;
        }

        var worldId = Singleton<GameWorld>.Instance.GetInstanceID();
        if (_worldId != worldId)
        {
            Reset();
            _worldId = worldId;
        }

        var key = bot.ProfileId ?? bot.GetInstanceID().ToString();
        if (!States.TryGetValue(key, out var state))
        {
            state = new ArmySurgeryState(bot);
            States.Add(key, state);
        }

        return state;
    }

    internal static void Reset()
    {
        States.Clear();
        _worldId = 0;
    }

    internal bool HasRecentThreat(BotOwner bot, float now)
    {
        var memory = bot?.Memory;
        if (memory == null)
        {
            return true;
        }

        var hitTime = memory.LastTimeHit;
        var wasHit = hitTime > _lastHitTime + 0.01f;
        if (hitTime > _lastHitTime)
        {
            _lastHitTime = hitTime;
        }

        var activeThreat = memory.HaveEnemy || memory.GoalEnemy?.IsVisible == true ||
            memory.IsUnderFire || bot.ShootData?.Shooting == true || wasHit;
        if (activeThreat)
        {
            _lastThreatAt = now;
        }

        return activeThreat || now - _lastThreatAt < QuietPeriod;
    }

    internal bool CanPrepare(BotOwner bot, float now)
    {
        if (ActionActive || now < NextEvaluationAt || HasRecentThreat(bot, now))
        {
            return false;
        }

        NextEvaluationAt = now + 1f;
        var medicine = bot.Medecine;
        if (medicine == null || medicine.Using)
        {
            return false;
        }

        var firstAid = medicine.FirstAid;
        firstAid?.Refresh();
        firstAid?.CheckParts();
        if (firstAid?.Have2Do == true)
        {
            return false;
        }

        var surgicalKit = medicine.SurgicalKit;
        if (surgicalKit == null)
        {
            return false;
        }

        surgicalKit.Refresh();
        surgicalKit.FindDamagedPart();
        return surgicalKit.HaveWork && surgicalKit.ShallStartUse();
    }

    internal void Prepare(float now)
    {
        ActionActive = true;
        TreatmentStarted = false;
        ActionEnded = false;
        ActionStartedAt = now;
        Plugin.Log?.LogInfo($"{_botName} is safe and is preparing Surv12 surgery.");
    }

    internal int BeginTreatment(float now)
    {
        TreatmentStarted = true;
        ActionStartedAt = now;
        Plugin.Log?.LogInfo($"{_botName} started Surv12 surgery.");
        return ++_attempt;
    }

    internal void Complete(int attempt, float now)
    {
        if (!ActionActive || attempt != _attempt)
        {
            return;
        }

        ActionActive = false;
        TreatmentStarted = false;
        ActionEnded = true;
        NextEvaluationAt = now + 1f;
        Plugin.Log?.LogInfo($"{_botName} completed Surv12 surgery.");
    }

    internal void Interrupt(float now, string reason)
    {
        if (!ActionActive)
        {
            return;
        }

        _attempt++;
        ActionActive = false;
        TreatmentStarted = false;
        ActionEnded = true;
        NextEvaluationAt = now + 5f;
        Plugin.Log?.LogInfo($"{_botName} interrupted Surv12 surgery: {reason}.");
    }
}
