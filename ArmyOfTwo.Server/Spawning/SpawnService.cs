using ArmyOfTwo.Server.Configuration;
using SPTarkov.Common.Models.Logging;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.DI;
using SPTarkov.Server.Core.Models.Eft.Common;
using SPTarkov.Server.Core.Models.Spt.Tables;

namespace ArmyOfTwo.Server.Spawning;

[Injectable(InjectionType = InjectionType.Singleton, TypePriority = OnLoadOrder.PostLoad + 100000)]
public sealed class SpawnService(
    LocationTable locationTable,
    ConfigService configService,
    ISptLogger<SpawnService> logger) : IOnLoad
{
    public Task OnLoadAsync(CancellationToken cancellationToken)
    {
        Apply(configService.Load().Spawns);
        return Task.CompletedTask;
    }

    public void Apply(SpawnConfig config)
    {
        RemoveExistingSpawns();

        if (!config.Enabled)
        {
            logger.Info("Army of Two: deployments are disabled.");
            return;
        }

        foreach (var entry in config.Maps.Where(entry => entry.Value.Enabled))
        {
            AddSpawn(entry.Key, entry.Value);
        }
    }

    private void RemoveExistingSpawns()
    {
        foreach (var location in locationTable.GetDictionary().Values)
        {
            location.Base?.BossLocationSpawn?.RemoveAll(spawn => string.Equals(
                spawn.BossName,
                ModInfo.RookBotKey,
                StringComparison.OrdinalIgnoreCase));
        }
    }

    private void AddSpawn(string mapName, MapSpawnConfig mapConfig)
    {
        var locationKey = locationTable.GetMappedKey(mapName);
        if (!locationTable.GetDictionary().TryGetValue(locationKey, out var location))
        {
            logger.Warning($"Army of Two: location '{mapName}' was not found; spawn skipped.");
            return;
        }

        var spawns = location.Base.BossLocationSpawn ??= [];
        var zones = GetEnabledZones(mapConfig);

        if (zones.Count == 0)
        {
            var knightSpawn = spawns.FirstOrDefault(spawn => string.Equals(
                spawn.BossName,
                "bossKnight",
                StringComparison.OrdinalIgnoreCase));

            if (!string.IsNullOrWhiteSpace(knightSpawn?.BossZone))
            {
                zones = knightSpawn.BossZone
                    .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                    .Where(SpawnZoneRules.IsAllowed)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();
            }
        }

        if (zones.Count == 0)
        {
            logger.Warning($"Army of Two: '{mapName}' has no selected spawn zones; spawn skipped.");
            return;
        }

        var chance = Math.Clamp(mapConfig.Chance, 0, 100);
        spawns.Add(new BossLocationSpawn
        {
            BossName = ModInfo.RookBotKey,
            BossChance = chance,
            BossDifficulty = "normal",
            BossEscortAmount = "1",
            BossEscortDifficulty = "normal",
            BossEscortType = ModInfo.TombstoneBotKey,
            IsBossPlayer = false,
            BossZone = string.Join(',', zones),
            Delay = 0,
            ForceSpawn = chance >= 100,
            IgnoreMaxBots = true,
            IsRandomTimeSpawn = false,
            SpawnMode = ["regular", "pve"],
            Supports = [],
            Time = -1,
            TriggerId = string.Empty,
            TriggerName = string.Empty
        });

        logger.Info(
            $"Army of Two: configured Rook and Tombstone on '{mapName}' " +
            $"across {zones.Count} zone(s) with a {chance}% spawn chance.");
    }

    private static List<string> GetEnabledZones(MapSpawnConfig mapConfig)
    {
        var disabledZones = new HashSet<string>(
            mapConfig.DisabledZones,
            StringComparer.OrdinalIgnoreCase);

        return mapConfig.Zones
            .Where(zone => !string.IsNullOrWhiteSpace(zone)
                && SpawnZoneRules.IsAllowed(zone)
                && !disabledZones.Contains(zone))
            .Select(zone => zone.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }
}
