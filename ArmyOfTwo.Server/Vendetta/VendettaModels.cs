using System.Text.Json.Serialization;
using SPTarkov.Server.Core.Models.Utils;

namespace ArmyOfTwo.Server.Vendetta;

public enum VendettaEventLevel
{
    None,
    Encounter,
    Damage,
    Kill
}

public sealed class VendettaProfileState
{
    public int Heat { get; set; }
    public bool Marked { get; set; }
    public bool ThreatMessageSent { get; set; }
    public string ActiveRaidId { get; set; } = string.Empty;
    public string LastProcessedRaidId { get; set; } = string.Empty;
    public bool HuntActive { get; set; }
    public VendettaEventLevel RookEventLevel { get; set; }
    public VendettaEventLevel TombstoneEventLevel { get; set; }
    public bool RookKilledDuringHunt { get; set; }
    public bool TombstoneKilledDuringHunt { get; set; }
    public HashSet<string> PreRaidMaskItemIds { get; set; } = [];
}

public sealed class VendettaRaidStatus
{
    [JsonPropertyName("heat")]
    public int Heat { get; set; }

    [JsonPropertyName("marked")]
    public bool Marked { get; set; }

    [JsonPropertyName("huntActive")]
    public bool HuntActive { get; set; }

    [JsonPropertyName("directHuntActive")]
    public bool DirectHuntActive { get; set; }

    [JsonPropertyName("location")]
    public string Location { get; set; } = string.Empty;
}

public sealed class VendettaEventRequest : IRequestData
{
    [JsonPropertyName("boss")]
    public string Boss { get; set; } = string.Empty;

    [JsonPropertyName("event")]
    public string Event { get; set; } = string.Empty;

    [JsonPropertyName("killedByPlayer")]
    public bool KilledByPlayer { get; set; }
}
