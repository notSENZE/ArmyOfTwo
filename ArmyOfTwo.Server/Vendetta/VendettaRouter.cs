using System.Text.Json;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.DI;
using SPTarkov.Server.Core.Models.Eft.Common;
using SPTarkov.Server.Core.Utils;

namespace ArmyOfTwo.Server.Vendetta;

[Injectable(InjectionType.Singleton, TypePriority = OnLoadOrder.Routers - 1)]
public sealed class VendettaRouter : StaticRouter
{
    public VendettaRouter(JsonUtil jsonUtil, VendettaService vendettaService)
        : base(jsonUtil,
        [
            new RouteAction<EmptyRequestData>(
                "/army-of-two/vendetta/status",
                async (_, _, sessionId, _, cancellationToken) =>
                {
                    var status = await vendettaService
                        .GetStatusAsync(sessionId, cancellationToken)
                        .ConfigureAwait(false);
                    return JsonSerializer.Serialize(status);
                }),
            new RouteAction<VendettaEventRequest>(
                "/army-of-two/vendetta/event",
                async (_, request, sessionId, _, cancellationToken) =>
                {
                    var status = await vendettaService
                        .RecordEventAsync(sessionId, request, cancellationToken)
                        .ConfigureAwait(false);
                    return JsonSerializer.Serialize(status);
                })
        ])
    {
    }
}
