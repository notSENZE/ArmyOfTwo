using ArmyOfTwo.Server.Configuration;
using ArmyOfTwo.Server.Vendetta;
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
        SpawnService spawnService,
        VendettaService vendettaService)
        : base(jsonUtil,
        [
            new RouteAction<StartLocalRaidRequestData>(
                "/client/match/local/start",
                async (_, request, sessionId, output, cancellationToken) =>
                {
                    var status = await vendettaService
                        .PrepareRaidAsync(sessionId, request, cancellationToken)
                        .ConfigureAwait(false);
                    spawnService.Apply(
                        configService.Load().Spawns,
                        status.HuntActive ? request.Location : null);
                    return output ?? string.Empty;
                }),
            new RouteAction<EndLocalRaidRequestData>(
                "/client/match/local/end",
                async (_, request, sessionId, output, cancellationToken) =>
                {
                    await vendettaService
                        .ProcessRaidEndAsync(sessionId, request, cancellationToken)
                        .ConfigureAwait(false);
                    return output ?? string.Empty;
                })
        ])
    {
    }
}
