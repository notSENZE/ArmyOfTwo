using SAIN.Preset.Shared.Models.Preset.Personalities;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.DI;
using SainConfigService = SAINServerMod.Services.ConfigService;

namespace ArmyOfTwo.Server;

[Injectable(InjectionType.Singleton, TypePriority = OnLoadOrder.Preload + 6)]
public sealed class SainPersonalityRegistration(SainConfigService sainConfigService) : IOnLoad
{
    public Task OnLoadAsync(CancellationToken cancellationToken)
    {
        var personalities = sainConfigService.NicknamesModel.NicknamePersonalities;
        personalities["Rook"] = EPersonality.GigaChad;
        personalities["Tombstone"] = EPersonality.SnappingTurtle;

        return Task.CompletedTask;
    }
}
