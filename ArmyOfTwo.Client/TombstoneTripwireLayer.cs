using DrakiaXYZ.BigBrain.Brains;
using EFT;
using UnityEngine;

namespace ArmyOfTwo.Client;

public sealed class TombstoneTripwireLayer : CustomLayer
{
    public TombstoneTripwireLayer(BotOwner botOwner, int priority) : base(botOwner, priority)
    {
    }

    public override string GetName() => "ArmyOfTwo_TombstoneTripwire";

    public override bool IsActive()
    {
        if (!Plugin.TripwiresEnabled || BotOwner == null || BotOwner.IsDead)
        {
            return false;
        }

        var state = TombstoneTripwireState.Get(BotOwner);
        if (state == null)
        {
            return false;
        }

        var now = Time.time;
        var threat = state.ObserveCombat(BotOwner, now);
        if (state.PlacementInFlight)
        {
            if (threat || now - state.ActionStartedAt > 8f)
            {
                state.EndAction(now);
            }
            return !state.ActionEnded;
        }

        if (state.PlanReady)
        {
            if (threat || now - state.ActionStartedAt > 3f ||
                !TombstoneTripwireEquipment.IsSafe(BotOwner, out _))
            {
                state.EndAction(now);
            }
            return !state.ActionEnded;
        }

        if (threat)
        {
            return false;
        }

        if (state.PlacementsRemaining <= 0 || state.CombatPaused)
        {
            return false;
        }

        if (!state.SuppliesReady)
        {
            if (now < state.NextSupplyAttemptAt)
            {
                return false;
            }

            state.SuppliesReady = TombstoneTripwireEquipment.EnsureOneSet(BotOwner);
            state.NextSupplyAttemptAt = now + 2f;
            if (!state.SuppliesReady)
            {
                state.ReportBlocked("tripwire supplies are unavailable", now);
                return false;
            }
            Plugin.Log?.LogInfo("Tombstone has a tripwire set ready.");
        }

        if (!state.CanPrepare(BotOwner, now, out var reason))
        {
            state.ReportBlocked(reason, now);
            return false;
        }

        if (!TombstoneTripwireEquipment.IsSafe(BotOwner, out reason))
        {
            state.ReportBlocked(reason, now);
            return false;
        }

        state.NextEvaluationAt = now + 0.5f;
        if (!TombstoneTripwireEquipment.TryFindPlacement(
                BotOwner, out var from, out var to, out var placementKind, out reason))
        {
            state.NextEvaluationAt = now + 2f;
            state.ReportBlocked(reason, now);
            return false;
        }

        state.Prepare(from, to, placementKind, now);
        Plugin.Log?.LogInfo(
            $"Tombstone found a safe tripwire position using {placementKind} search and is preparing to plant it.");
        return true;
    }

    public override CustomLayer.Action GetNextAction()
    {
        return new CustomLayer.Action(typeof(TombstoneTripwireLogic), "Prepare tripwire while safe");
    }

    public override bool IsCurrentActionEnding()
    {
        if (BotOwner == null || BotOwner.IsDead)
        {
            return true;
        }

        var state = TombstoneTripwireState.Get(BotOwner);
        if (state == null || state.ActionEnded)
        {
            return true;
        }

        var now = Time.time;
        var threat = state.ObserveCombat(BotOwner, now);
        if (threat || now - state.ActionStartedAt > 8f)
        {
            state.EndAction(now);
            return true;
        }

        return false;
    }

    public override void Stop()
    {
        if (BotOwner == null)
        {
            return;
        }

        var state = TombstoneTripwireState.Get(BotOwner);
        if (state != null && !state.ActionEnded)
        {
            if (state.PlanReady)
            {
                Plugin.Log?.LogInfo("Tombstone's prepared tripwire action was interrupted by another AI layer.");
            }
            state.EndAction(Time.time);
        }
    }
}
