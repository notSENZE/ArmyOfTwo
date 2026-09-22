using Comfort.Common;
using EFT;
using EFT.Ballistics;
using UnityEngine;

namespace ArmyOfTwo.Client;

internal sealed class VendettaBotTracker : MonoBehaviour
{
    private BotOwner _bot;
    private Player _botPlayer;
    private string _boss;
    private bool _encounterReported;
    private bool _damageReported;
    private bool _deathReported;
    private float _nextEncounterCheckAt;

    internal void Init(BotOwner bot, string boss)
    {
        if (_botPlayer != null)
        {
            Unsubscribe();
        }

        _bot = bot;
        _botPlayer = bot?.GetPlayer;
        _boss = boss;
        if (_botPlayer == null)
        {
            return;
        }

        _botPlayer.BeingHitAction += OnHit;
        _botPlayer.OnPlayerDead += OnDead;
    }

    private void Update()
    {
        if (_encounterReported || _bot == null || _bot.IsDead ||
            Time.time < _nextEncounterCheckAt || !Singleton<GameWorld>.Instantiated)
        {
            return;
        }

        _nextEncounterCheckAt = Time.time + 0.25f;
        var enemy = _bot.Memory?.GoalEnemy;
        if (enemy?.HaveSeenPersonal == true && IsLocalPlayer(enemy.Person))
        {
            _encounterReported = true;
            VendettaClientBridge.ReportEvent(_boss, "encounter");
            Plugin.Log?.LogInfo($"Vendetta heat: {_boss} encountered the local player.");
        }
    }

    private void OnHit(DamageInfo damageInfo, EBodyPart bodyPart, float damage)
    {
        if (_damageReported || damage <= 0f || !IsLocalPlayer(damageInfo.Player?.iPlayer))
        {
            return;
        }

        _damageReported = true;
        VendettaClientBridge.ReportEvent(_boss, "damage");
        Plugin.Log?.LogInfo($"Vendetta heat: {_boss} was damaged by the local player.");
    }

    private void OnDead(Player victim, IPlayer aggressor, DamageInfo damageInfo, EBodyPart bodyPart)
    {
        if (_deathReported)
        {
            return;
        }

        _deathReported = true;
        var killedByPlayer = IsLocalPlayer(aggressor);
        VendettaClientBridge.ReportEvent(_boss, "kill", killedByPlayer, waitForServer: true);
        Plugin.Log?.LogInfo(
            $"Vendetta hunt: {_boss} died" +
            (killedByPlayer ? " to the local player." : "."));
    }

    private static bool IsLocalPlayer(IPlayer player)
    {
        if (player == null || player.IsAI || !Singleton<GameWorld>.Instantiated)
        {
            return false;
        }

        var localPlayer = Singleton<GameWorld>.Instance.MainPlayer;
        return localPlayer != null &&
            (player.IsYourPlayer || player.ProfileId == localPlayer.ProfileId);
    }

    private void OnDestroy()
    {
        Unsubscribe();
    }

    private void Unsubscribe()
    {
        if (_botPlayer == null)
        {
            return;
        }

        _botPlayer.BeingHitAction -= OnHit;
        _botPlayer.OnPlayerDead -= OnDead;
        _botPlayer = null;
    }
}
