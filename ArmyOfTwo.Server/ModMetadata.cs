using SPTarkov.Server.Core.Models.Spt.Mod;
using Range = SemanticVersioning.Range;

namespace ArmyOfTwo.Server;

public record ModMetadata : IModMetadata
{
    public string ModGuid { get; init; } = ModInfo.Guid;
    public string Name { get; init; } = ModInfo.Name;
    public string Author { get; init; } = ModInfo.Author;
    public List<string>? Contributors { get; init; } = [];
    public SemanticVersioning.Version Version { get; init; } = new(0, 8, 0);
    public Range SptVersion { get; init; } = new("~4.1.5");
    public List<string>? Incompatibilities { get; init; }
    public Dictionary<string, Range>? ModDependencies { get; init; } = new()
    {
        { "com.morebotsapi.tacticaltoaster", new Range("~2.1.1") },
        { "me.sol.sain", new Range("~4.5.1") },
        { "com.wtt.commonlib", new Range("~3.0.6") },
        { "com.wtt.contentbackport", new Range("~2.0.2") }
    };
    public string? Url { get; init; }
    public string License { get; init; } = "MIT";
    public bool HasPrepatcher { get; init; } = false;
}
