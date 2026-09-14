using ArmyOfTwo.Server.Configuration;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.DI;
using SPTarkov.Server.Core.Models.Eft.Match;
using SPTarkov.Server.Core.Utils;

namespace ArmyOfTwo.Server.Spawning;

[Injectable(InjectionType.Singleton, TypePriority = OnLoadOrder.Routers - 1)]
public sealed class RaidSpawnPersistenceRouter : StaticRouter
{
    public RaidSpawnPersistenceRouter(
        JsonUtil jsonUtil,
        ConfigService configService,
        SpawnService spawnService)
        : base(jsonUtil,
        [
            new RouteAction<StartLocalRaidRequestData>(
                "/client/match/local/start",
                (_, _, _, output, _) =>
                {
                    // Restore our entries before SPT creates the location data for the current raid.
                    spawnService.Apply(configService.Load().Spawns);
                    return new ValueTask<string>(output ?? string.Empty);
                })
        ])
    {
    }
}
