using DrakiaXYZ.BigBrain.Brains;
using EFT;
using UnityEngine;

namespace ArmyOfTwo.Client;

public sealed class ArmySurgeryLogic : CustomLogic
{
    private ArmySurgeryState _state;

    public ArmySurgeryLogic(BotOwner botOwner) : base(botOwner)
    {
    }

    public override void Start()
    {
        if (BotOwner != null)
        {
            _state = ArmySurgeryState.Get(BotOwner);
        }
    }

    public override void Update(CustomLayer.ActionData data)
    {
        if (BotOwner == null || BotOwner.IsDead || _state == null ||
            _state.ActionEnded || _state.TreatmentStarted)
        {
            return;
        }

        var now = Time.time;
        if (_state.HasRecentThreat(BotOwner, now))
        {
            _state.Interrupt(now, "combat resumed");
            return;
        }

        var surgicalKit = BotOwner.Medecine?.SurgicalKit;
        if (surgicalKit == null || !surgicalKit.ShallStartUse())
        {
            _state.Interrupt(now, "the Surv12 was unavailable");
            return;
        }

        BotOwner.SetPose(0f);
        BotOwner.Sprint(false, true);
        if (BotOwner.WeaponManager?.Reload?.Reloading == true)
        {
            BotOwner.WeaponManager.Reload.TryStopReload();
        }

        var attempt = _state.BeginTreatment(now);
        surgicalKit.ApplyToCurrentPart(() => _state.Complete(attempt, Time.time));
        if (!surgicalKit.Using)
        {
            _state.Interrupt(now, "the treatment could not start");
        }
    }

    public override void Stop()
    {
    }
}
