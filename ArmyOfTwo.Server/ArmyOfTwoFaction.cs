using MoreBotsServer.Services;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.DI;
using SPTarkov.Server.Core.Models.Eft.Common;

namespace ArmyOfTwo.Server;

[Injectable(InjectionType = InjectionType.Singleton, TypePriority = MoreBotsServer.MoreBotsLoadOrder.LoadFactions)]
public sealed class ArmyOfTwoFaction(FactionService factionService) : IOnLoad
{
    public Task OnLoadAsync(CancellationToken cancellationToken)
    {
        factionService.Factions[ModInfo.FactionName] = new Faction
        {
            Name = ModInfo.FactionName,
            BotTypes =
            [
                (WildSpawnType)ModInfo.RookWildSpawnType,
                (WildSpawnType)ModInfo.TombstoneWildSpawnType
            ]
        };

        return Task.CompletedTask;
    }
}
