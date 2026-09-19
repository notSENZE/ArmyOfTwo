using DrakiaXYZ.BigBrain.Brains;
using EFT;
using UnityEngine;

namespace ArmyOfTwo.Client;

public sealed class TombstoneTripwireLogic : CustomLogic
{
    private TombstoneTripwireState _state;

    public TombstoneTripwireLogic(BotOwner botOwner) : base(botOwner)
    {
    }

    public override void Start()
    {
        if (BotOwner != null)
        {
            _state = TombstoneTripwireState.Get(BotOwner);
        }
    }

    public override void Update(CustomLayer.ActionData data)
    {
        if (BotOwner == null || BotOwner.IsDead || _state == null || _state.ActionEnded)
        {
            return;
        }

        var now = Time.time;
        if (!_state.PlanReady || _state.PlacementInFlight)
        {
            return;
        }

        if (_state.ObserveCombat(BotOwner, now) ||
            !TombstoneTripwireEquipment.IsSafe(BotOwner, out _))
        {
            _state.EndAction(now);
            return;
        }

        if (!TombstoneTripwireEquipment.TryGetSupplies(BotOwner, out var grenade, out var kit))
        {
            _state.SuppliesReady = false;
            _state.EndAction(now);
            return;
        }

        var controller = BotOwner.GetPlayer?.InventoryController;
        if (controller == null)
        {
            _state.EndAction(now);
            return;
        }

        var attempt = _state.BeginPlacement(now);
        try
        {
            controller.PlantTripwire(
                grenade,
                kit,
                _state.From,
                _state.To,
                result => _state.FinishPlacement(attempt, result?.Succeed == true, Time.time));
        }
        catch (System.Exception exception)
        {
            Plugin.Log?.LogWarning($"Tombstone tripwire placement failed: {exception.Message}");
            _state.FinishPlacement(attempt, false, now);
        }
    }

    public override void Stop() { }
}
