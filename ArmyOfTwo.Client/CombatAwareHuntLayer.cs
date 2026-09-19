using EFT;
using MoreBotsAPI.Behavior.Layers;
using UnityEngine;

namespace ArmyOfTwo.Client;

internal sealed class CombatAwareHuntLayer : HuntTargetLayer
{
    public CombatAwareHuntLayer(BotOwner botOwner, int priority) : base(botOwner, priority)
    {
    }

    public override bool IsActive()
    {
        var memory = BotOwner.Memory;
        if (memory != null &&
            (memory.GoalEnemy != null || memory.IsUnderFire ||
             (memory.LastTimeHit > 0f && Time.time - memory.LastTimeHit < 20f)))
        {
            return false;
        }

        return base.IsActive();
    }
}
