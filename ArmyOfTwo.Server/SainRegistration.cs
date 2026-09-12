using MoreBotsServer.Interop;
using MoreBotsServer.Models;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.DI;

namespace ArmyOfTwo.Server;

[Injectable(TypePriority = OnLoadOrder.Preload + 2)]
public sealed class SainRegistration(SainInteropRegistration sainInterop) : IOnLoad
{
    public Task OnLoadAsync(CancellationToken cancellationToken)
    {
        sainInterop.RegisterBotType(new MoreBotsSainBotTypeRegistration
        {
            WildSpawnType = ModInfo.RookWildSpawnType,
            BotDbKey = ModInfo.RookBotKey,
            Name = "Rook",
            Section = "Army of Two",
            Description = "The aggressive leader of Army of Two.",
            DifficultyModifier = 1f,
            BaseBrain = "Knight",
            BrainsToApply = ["Knight"]
        });

        sainInterop.RegisterBotType(new MoreBotsSainBotTypeRegistration
        {
            WildSpawnType = ModInfo.TombstoneWildSpawnType,
            BotDbKey = ModInfo.TombstoneBotKey,
            Name = "Tombstone",
            Section = "Army of Two",
            Description = "Rook's ranged support specialist.",
            DifficultyModifier = 1f,
            BaseBrain = "BirdEye",
            BrainsToApply = ["BirdEye"]
        });

        return Task.CompletedTask;
    }
}
