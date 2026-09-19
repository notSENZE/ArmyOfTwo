using Comfort.Common;
using EFT;
using MoreBotsAPI.Components;
using UnityEngine;

namespace ArmyOfTwo.Client;

internal sealed class TombstoneSoloHunt : MonoBehaviour
{
    private BotOwner _tombstone;
    private IBossToFollow _rook;
    private BotHuntManager _hunt;
    private bool _hasSeenRook;
    private bool _solo;
    private float _nextCheckAt;

    internal void Init(BotOwner tombstone, BotHuntManager hunt)
    {
        _tombstone = tombstone;
        _hunt = hunt;
    }

    private void Update()
    {
        if (_tombstone == null || _tombstone.IsDead || _hunt == null ||
            Time.time < _nextCheckAt)
        {
            return;
        }

        _nextCheckAt = Time.time + 2f;
        if (!_hasSeenRook)
        {
            _rook = _tombstone.BotFollower?.BossToFollow;
            _hasSeenRook = _rook != null;
            if (!_hasSeenRook)
            {
                return;
            }
        }

        if (!_solo && (_rook == null || !_rook.IsAlive))
        {
            _solo = true;
            _hunt.ignoreRegroup = true;
            _hunt.shouldRegroup = false;
            _hunt.isRegrouping = false;
            _hunt.shouldSearch = false;
            Plugin.Log?.LogInfo("Rook is dead; Tombstone has stopped regrouping and is hunting independently.");
        }

        if (_solo && !_hunt.HasHuntTarget() && Singleton<GameWorld>.Instantiated)
        {
            MonoBehaviourSingleton<HuntManager>.Instance.FindNewHuntTarget(_hunt);
            if (_hunt.HasHuntTarget())
            {
                _hunt.knownLocation = _hunt.huntTarget.Position;
            }
        }
    }
}
