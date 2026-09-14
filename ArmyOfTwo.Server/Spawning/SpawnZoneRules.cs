namespace ArmyOfTwo.Server.Spawning;

internal static class SpawnZoneRules
{
    public static bool IsAllowed(string zone)
    {
        return !zone.Contains("snipe", StringComparison.OrdinalIgnoreCase);
    }
}
