using System;
using System.Collections.Generic;
using Comfort.Common;
using EFT;
using UnityEngine;

namespace ArmyOfTwo.Client;

internal sealed class TombstoneTripwireState
{
    private const int MaximumPlacements = 7;
    private static readonly Dictionary<string, TombstoneTripwireState> States =
        new Dictionary<string, TombstoneTripwireState>(StringComparer.Ordinal);
    private static int _worldId;

    private float _lastHitTime;
    private readonly float _spawnedAt;
    private readonly string _profileId;
    private Vector3 _lastPlacementPosition;
    private bool _hasPlaced;
    private bool _combatPaused;
    private float _lastCombatAt;
    private int _attempt;
    private float _lastBlockLogAt = -100f;
    private string _lastBlockReason;

    private TombstoneTripwireState(BotOwner bot)
    {
        _lastHitTime = bot.Memory?.LastTimeHit ?? 0f;
        _spawnedAt = Time.time;
        _profileId = bot.ProfileId;
    }

    internal int PlacementsRemaining { get; private set; } = MaximumPlacements;
    internal bool SuppliesReady { get; set; }
    internal float NextSupplyAttemptAt { get; set; }
    internal bool PlanReady { get; private set; }
    internal bool PlacementInFlight { get; private set; }
    internal bool ActionEnded { get; private set; }
    internal float ActionStartedAt { get; private set; }
    internal float NextEvaluationAt { get; set; }
    internal Vector3 From { get; private set; }
    internal Vector3 To { get; private set; }
    internal string PlacementKind { get; private set; }
    internal bool CombatPaused => _combatPaused;
    internal Vector3 BotPosition { get; private set; }

    internal static TombstoneTripwireState Get(BotOwner bot)
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
            state = new TombstoneTripwireState(bot);
            States.Add(key, state);
        }

        return state;
    }

    internal static void Reset()
    {
        States.Clear();
        TombstoneTripwireAvoidance.Reset();
        _worldId = 0;
    }

    internal bool ObserveCombat(BotOwner bot, float now)
    {
        BotPosition = bot.Position;
        var memory = bot.Memory;
        if (memory == null)
        {
            return false;
        }

        var hitTime = memory.LastTimeHit;
        var wasHit = hitTime > _lastHitTime + 0.01f;
        if (hitTime > _lastHitTime)
        {
            _lastHitTime = hitTime;
        }

        var combatStarted = memory.GoalEnemy?.IsVisible == true || memory.IsUnderFire ||
            bot.ShootData?.Shooting == true || wasHit;
        if (combatStarted)
        {
            _lastCombatAt = now;
            if (!_combatPaused)
            {
                Plugin.Log?.LogInfo("Tombstone stopped preparing tripwires because combat began.");
            }
            _combatPaused = true;
        }

        if (_combatPaused && !combatStarted && !memory.HaveEnemy && now - _lastCombatAt >= 20f)
        {
            _combatPaused = false;
            Plugin.Log?.LogInfo("Tombstone can resume tripwire preparation after losing contact.");
        }

        return _combatPaused || memory.HaveEnemy;
    }

    internal bool CanPrepare(BotOwner bot, float now, out string reason)
    {
        reason = null;
        if (!SuppliesReady || PlacementsRemaining <= 0 || _combatPaused || PlacementInFlight)
        {
            return false;
        }

        if (now < _spawnedAt + 20f)
        {
            reason = "waiting for the initial setup window";
            return false;
        }

        if (now < NextEvaluationAt)
        {
            reason = "waiting for the placement cooldown";
            return false;
        }

        var requiredDistance = PlacementsRemaining >= 6 ? 7f : 10f;
        if (_hasPlaced && Horizontal(bot.Position - _lastPlacementPosition).sqrMagnitude <
            requiredDistance * requiredDistance)
        {
            reason = $"waiting to move {requiredDistance:0}m away from the last tripwire";
            return false;
        }

        return true;
    }

    internal void ReportBlocked(string reason, float now)
    {
        if (string.IsNullOrEmpty(reason))
        {
            return;
        }

        var interval = reason == _lastBlockReason ? 60f : 10f;
        if (now - _lastBlockLogAt >= interval)
        {
            Plugin.Log?.LogInfo($"Tombstone tripwire preparation deferred: {reason}.");
            _lastBlockLogAt = now;
            _lastBlockReason = reason;
        }
    }

    internal void Prepare(Vector3 from, Vector3 to, string placementKind, float now)
    {
        From = from;
        To = to;
        PlacementKind = placementKind;
        PlanReady = true;
        ActionEnded = false;
        ActionStartedAt = now;
    }

    internal int BeginPlacement(float now)
    {
        PlanReady = false;
        PlacementInFlight = true;
        ActionStartedAt = now;
        return ++_attempt;
    }

    internal void FinishPlacement(int attempt, bool success, float now)
    {
        if (!PlacementInFlight || attempt != _attempt)
        {
            return;
        }

        PlacementInFlight = false;
        SuppliesReady = false;
        if (success)
        {
            TombstoneTripwireAvoidance.Register(From, To, _profileId);
            PlacementsRemaining--;
            _lastPlacementPosition = BotPosition;
            _hasPlaced = true;
            var placementsMade = MaximumPlacements - PlacementsRemaining;
            Plugin.Log?.LogInfo(
                $"Tombstone placed tripwire {placementsMade}/{MaximumPlacements} using {PlacementKind} search; " +
                $"{PlacementsRemaining} remaining this raid.");
        }
        else
        {
            Plugin.Log?.LogWarning("Tombstone could not place his tripwire.");
        }
        CompleteAction(now);
    }

    private void CompleteAction(float now)
    {
        ActionEnded = true;
        NextEvaluationAt = now + (PlacementsRemaining >= 6 ? 8f : 15f);
    }

    internal void EndAction(float now)
    {
        PlanReady = false;
        if (!PlacementInFlight)
        {
            CompleteAction(now);
        }
    }

    private static Vector3 Horizontal(Vector3 value)
    {
        value.y = 0f;
        return value;
    }

}
