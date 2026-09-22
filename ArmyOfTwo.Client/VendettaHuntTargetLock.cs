using UnityEngine;
using MoreBotsAPI.Components;

namespace ArmyOfTwo.Client;

internal sealed class VendettaHuntTargetLock : MonoBehaviour
{
    private HuntManager _huntManager;
    private BotHuntManager _hunter;

    internal void Init(HuntManager huntManager, BotHuntManager hunter)
    {
        _huntManager = huntManager;
        _hunter = hunter;
    }

    private void Update()
    {
        if (!VendettaClientBridge.DirectHuntActive)
        {
            return;
        }

        VendettaClientBridge.MaintainLocalPlayerTarget(_huntManager, _hunter);
    }
}
