using System.Reflection;
using MoreBotsServer.Services;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.DI;

namespace ArmyOfTwo.Server;

[Injectable(InjectionType = InjectionType.Singleton, TypePriority = MoreBotsServer.MoreBotsLoadOrder.LoadBots)]
public sealed class ArmyOfTwoContent(
    MoreBotsServer.MoreBotsAPI moreBots,
    MoreBotsCustomBotTypeService customBotTypeService,
    FactionService factionService,
    WTTServerCommonLib.WTTServerCommonLib commonLib) : IOnLoad
{
    public async Task OnLoadAsync(CancellationToken cancellationToken)
    {
        var assembly = Assembly.GetExecutingAssembly();

        await commonLib.CustomItemServiceExtended.CreateCustomItems(assembly);
        await commonLib.CustomWeaponPresetService.CreateCustomWeaponPresets(assembly);
        await moreBots.LoadBots(assembly);

        customBotTypeService.AddCustomWildSpawnTypeNames(new Dictionary<int, string>
        {
            { ModInfo.RookWildSpawnType, ModInfo.RookBotKey },
            { ModInfo.TombstoneWildSpawnType, ModInfo.TombstoneBotKey }
        });

        var botTypes = new[] { ModInfo.RookBotKey, ModInfo.TombstoneBotKey };
        factionService.AddFriendlyByFaction(botTypes, ModInfo.FactionName);
        factionService.AddRevengeByFaction(botTypes, ModInfo.FactionName);
        factionService.AddEnemyByFaction(botTypes, "pmcs");
        factionService.AddEnemyByFaction(botTypes, "scavs");
        factionService.AddEnemyByFaction("pmcs", ModInfo.FactionName);
        factionService.AddEnemyByFaction("scavs", ModInfo.FactionName);
    }
}
