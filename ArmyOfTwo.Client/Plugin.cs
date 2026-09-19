using System.Collections.Generic;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using Comfort.Common;
using DrakiaXYZ.BigBrain.Brains;
using EFT;
using MoreBotsAPI.Behavior.Layers;
using MoreBotsAPI.Components;

namespace ArmyOfTwo.Client;

[BepInPlugin(Guid, "Army of Two", Version)]
[BepInDependency("com.SPT.core", "4.1.0")]
[BepInDependency("xyz.drakia.bigbrain", "1.4.0")]
[BepInDependency("com.morebotsapi.tacticaltoaster")]
public sealed class Plugin : BaseUnityPlugin
{
    public const string Guid = "spt.senze.armyoftwo";
    public const string Version = "0.9.10";
    private const int RookWildSpawnType = 658400;
    private const int TombstoneWildSpawnType = 658401;

    private ConfigEntry<bool> _enableTripwires;
    private HuntManager _huntManager;

    internal static Plugin Instance { get; private set; }
    internal static ManualLogSource Log { get; private set; }
    internal static bool TripwiresEnabled => Instance?._enableTripwires?.Value == true;

    private void Awake()
    {
        Instance = this;
        Log = Logger;
        _enableTripwires = Config.Bind(
            "Tombstone",
            "Enable Tripwires",
            true,
            "Allow Tombstone to place up to seven tripwires when out of combat.");

        var armyRoles = new List<WildSpawnType>
        {
            (WildSpawnType)RookWildSpawnType,
            (WildSpawnType)TombstoneWildSpawnType
        };
        var brains = new List<string> { "PMC", "ExUsec", "Assault", "PmcUsec", "PmcBear", "PmcUSEC", "PmcBEAR" };

        _huntManager = MonoBehaviourSingleton<HuntManager>.Instance;
        _huntManager.AddHuntRoles(armyRoles, new List<WildSpawnType> { WildSpawnType.pmcUSEC, WildSpawnType.pmcBEAR });
        _huntManager.AddHuntSides(armyRoles, new List<EPlayerSide> { EPlayerSide.Usec, EPlayerSide.Bear });
        _huntManager.OnBotHuntInit += OnBotHuntInit;
        BrainManager.AddCustomLayer(typeof(CombatAwareHuntLayer), brains, 10, armyRoles);

        BrainManager.AddCustomLayer(typeof(ArmySurgeryLayer), brains, 75, armyRoles);

        BrainManager.AddCustomLayer(
            typeof(TombstoneTripwireLayer),
            brains,
            74,
            new List<WildSpawnType> { (WildSpawnType)TombstoneWildSpawnType });
        Logger.LogInfo($"Army of Two {Version}: group hunt, safe surgery, and Tombstone tripwires registered.");
    }

    private void OnDestroy()
    {
        if (_huntManager != null)
        {
            _huntManager.OnBotHuntInit -= OnBotHuntInit;
        }
        TombstoneTripwireState.Reset();
        ArmySurgeryState.Reset();
        Instance = null;
        Log = null;
    }

    private void OnBotHuntInit(BotHuntManager manager)
    {
        var bot = manager?.botOwner;
        var role = (int)(bot?.Profile?.Info?.Settings?.Role ?? 0);
        if (role != RookWildSpawnType && role != TombstoneWildSpawnType)
        {
            return;
        }

        var avoidance = bot.GetComponent<TombstoneTripwireAvoidance>();
        if (avoidance == null)
        {
            avoidance = bot.gameObject.AddComponent<TombstoneTripwireAvoidance>();
        }
        avoidance.Init(bot);

        if (role == TombstoneWildSpawnType)
        {
            var soloHunt = bot.GetComponent<TombstoneSoloHunt>();
            if (soloHunt == null)
            {
                soloHunt = bot.gameObject.AddComponent<TombstoneSoloHunt>();
            }
            soloHunt.Init(bot, manager);
        }
    }
}
