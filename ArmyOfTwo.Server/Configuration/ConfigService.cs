using System.Reflection;
using System.Text.Json;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.Helpers.Server;

namespace ArmyOfTwo.Server.Configuration;

[Injectable(InjectionType = InjectionType.Singleton)]
public sealed class ConfigService(ModHelper modHelper)
{
    private readonly object syncRoot = new();
    private ArmyOfTwoConfig? current;

    public string ModPath { get; } =
        modHelper.GetAbsolutePathToModFolder(Assembly.GetExecutingAssembly());

    public ArmyOfTwoConfig Load()
    {
        lock (syncRoot)
        {
            current ??= ReadFromDisk();
            return current;
        }
    }

    public void SaveSpawns(SpawnConfig spawns)
    {
        Normalize(spawns);

        lock (syncRoot)
        {
            var config = current ?? ReadFromDisk();
            config.Spawns = spawns;

            var json = JsonSerializer.Serialize(config, new JsonSerializerOptions
            {
                WriteIndented = true
            });

            File.WriteAllText(Path.Combine(ModPath, "config.jsonc"), json);
            current = config;
        }
    }

    private ArmyOfTwoConfig ReadFromDisk()
    {
        var path = Path.Combine(ModPath, "config.jsonc");
        var json = File.ReadAllText(path);

        using var document = JsonDocument.Parse(json, new JsonDocumentOptions
        {
            AllowTrailingCommas = true,
            CommentHandling = JsonCommentHandling.Skip
        });

        if (IsLegacyConfig(document.RootElement))
        {
            return ReadLegacyConfig(document.RootElement);
        }

        var config = JsonSerializer.Deserialize<ArmyOfTwoConfig>(json, new JsonSerializerOptions
        {
            AllowTrailingCommas = true,
            ReadCommentHandling = JsonCommentHandling.Skip,
            PropertyNameCaseInsensitive = true
        }) ?? new ArmyOfTwoConfig();

        Normalize(config.Spawns);
        return config;
    }

    private static bool IsLegacyConfig(JsonElement root)
    {
        return root.TryGetProperty("spawns", out var spawns)
            && spawns.TryGetProperty("maps", out var maps)
            && maps.ValueKind == JsonValueKind.Array;
    }

    private static ArmyOfTwoConfig ReadLegacyConfig(JsonElement root)
    {
        var result = new ArmyOfTwoConfig();
        var spawns = root.GetProperty("spawns");
        result.Spawns.Enabled = !spawns.TryGetProperty("enabled", out var enabled) || enabled.GetBoolean();

        var chance = spawns.TryGetProperty("chance", out var chanceElement)
            ? chanceElement.GetSingle()
            : 20;

        foreach (var map in spawns.GetProperty("maps").EnumerateArray())
        {
            var mapName = map.GetString();
            if (string.IsNullOrWhiteSpace(mapName))
            {
                continue;
            }

            result.Spawns.Maps[mapName] = new MapSpawnConfig
            {
                Enabled = true,
                Chance = chance
            };
        }

        Normalize(result.Spawns);
        return result;
    }

    private static void Normalize(SpawnConfig config)
    {
        var normalizedMaps = new Dictionary<string, MapSpawnConfig>(StringComparer.OrdinalIgnoreCase);

        foreach (var entry in config.Maps)
        {
            var mapName = entry.Key.Trim();
            if (string.IsNullOrWhiteSpace(mapName))
            {
                continue;
            }

            var map = entry.Value ?? new MapSpawnConfig();
            map.Chance = Math.Clamp(map.Chance, 0, 100);
            map.Zones = NormalizeZones(map.Zones);
            map.DisabledZones = NormalizeZones(map.DisabledZones)
                .Where(zone => map.Zones.Contains(zone, StringComparer.OrdinalIgnoreCase))
                .ToList();
            normalizedMaps[mapName] = map;
        }

        config.Maps = normalizedMaps;
    }

    private static List<string> NormalizeZones(IEnumerable<string>? zones)
    {
        return (zones ?? [])
            .Where(zone => !string.IsNullOrWhiteSpace(zone))
            .Select(zone => zone.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(zone => zone, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }
}
