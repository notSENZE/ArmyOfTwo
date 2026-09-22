using EFT;
using MoreBotsAPI.Behavior.Layers;

namespace ArmyOfTwo.Client;

internal sealed class VendettaDirectHuntLayer : HuntTargetLayer
{
    private bool _activationLogged;

    public VendettaDirectHuntLayer(BotOwner botOwner, int priority) : base(botOwner, priority)
    {
    }

    public override string GetName() => "ArmyOfTwo_VendettaDirectHunt";

    public override bool IsActive()
    {
        if (!VendettaClientBridge.DirectHuntActive)
        {
            return false;
        }

        if (BotOwner.Memory?.GoalEnemy?.IsVisible == true)
        {
            return false;
        }

        var active = base.IsActive();
        if (active && !_activationLogged)
        {
            _activationLogged = true;
            Plugin.Log?.LogInfo(
                $"Hunting Mode: {BotOwner.Profile?.Info?.Nickname ?? "Army of Two bot"} " +
                "is moving toward the local player.");
        }

        return active;
    }
}
