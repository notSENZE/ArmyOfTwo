using DrakiaXYZ.BigBrain.Brains;
using EFT;
using UnityEngine;

namespace ArmyOfTwo.Client;

public sealed class ArmySurgeryLayer : CustomLayer
{
    private const float TreatmentTimeout = 45f;

    public ArmySurgeryLayer(BotOwner botOwner, int priority) : base(botOwner, priority)
    {
    }

    public override string GetName() => "ArmyOfTwo_Surgery";

    public override bool IsActive()
    {
        if (BotOwner == null || BotOwner.IsDead)
        {
            return false;
        }

        var state = ArmySurgeryState.Get(BotOwner);
        if (state == null)
        {
            return false;
        }

        var now = Time.time;
        if (state.ActionActive)
        {
            if (state.HasRecentThreat(BotOwner, now))
            {
                CancelTreatment(state, now, "combat resumed");
                return false;
            }

            if (now - state.ActionStartedAt > TreatmentTimeout)
            {
                CancelTreatment(state, now, "treatment timed out");
                return false;
            }

            return true;
        }

        if (!state.CanPrepare(BotOwner, now))
        {
            return false;
        }

        state.Prepare(now);
        return true;
    }

    public override CustomLayer.Action GetNextAction()
    {
        return new CustomLayer.Action(typeof(ArmySurgeryLogic), "Use Surv12 while safe");
    }

    public override bool IsCurrentActionEnding()
    {
        if (BotOwner == null || BotOwner.IsDead)
        {
            return true;
        }

        var state = ArmySurgeryState.Get(BotOwner);
        if (state == null || state.ActionEnded)
        {
            return true;
        }

        var now = Time.time;
        if (state.HasRecentThreat(BotOwner, now))
        {
            CancelTreatment(state, now, "combat resumed");
            return true;
        }

        if (now - state.ActionStartedAt > TreatmentTimeout)
        {
            CancelTreatment(state, now, "treatment timed out");
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

        var state = ArmySurgeryState.Get(BotOwner);
        if (state?.ActionActive == true)
        {
            CancelTreatment(state, Time.time, "another AI action took priority");
        }
    }

    private void CancelTreatment(ArmySurgeryState state, float now, string reason)
    {
        if (state.TreatmentStarted)
        {
            BotOwner.Medecine?.SurgicalKit?.CancelCurrent();
        }

        state.Interrupt(now, reason);
    }
}
