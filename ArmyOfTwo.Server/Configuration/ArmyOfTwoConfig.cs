using System.Text.Json.Serialization;

namespace ArmyOfTwo.Server.Configuration;

public sealed class ArmyOfTwoConfig
{
    [JsonPropertyName("spawns")]
    public SpawnConfig Spawns { get; set; } = new();

    [JsonPropertyName("webUi")]
    public WebUiConfig WebUi { get; set; } = new();
}

public sealed class SpawnConfig
{
    [JsonPropertyName("enabled")]
    public bool Enabled { get; set; } = true;

    [JsonPropertyName("maps")]
    public Dictionary<string, MapSpawnConfig> Maps { get; set; } =
        new(StringComparer.OrdinalIgnoreCase);
}

public sealed class MapSpawnConfig
{
    [JsonPropertyName("enabled")]
    public bool Enabled { get; set; }

    [JsonPropertyName("chance")]
    public float Chance { get; set; } = 20;

    [JsonPropertyName("zones")]
    public List<string> Zones { get; set; } = [];

    [JsonPropertyName("disabledZones")]
    public List<string> DisabledZones { get; set; } = [];
}

public sealed class WebUiConfig
{
    [JsonPropertyName("enabled")]
    public bool Enabled { get; set; } = true;

    [JsonPropertyName("host")]
    public string Host { get; set; } = "127.0.0.1";

    [JsonPropertyName("port")]
    public int Port { get; set; } = 6971;
}
