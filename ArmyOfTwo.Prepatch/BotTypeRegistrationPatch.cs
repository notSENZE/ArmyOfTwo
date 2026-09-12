using System.Collections.Generic;
using Mono.Cecil;
using MoreBotsAPI;

namespace ArmyOfTwo.Prepatch;

public static class BotTypeRegistrationPatch
{
    private const int KnightBrain = 26;
    private const int BirdeyeBrain = 28;
    private const int RookId = 658400;
    private const int TombstoneId = 658401;

    private static readonly List<int> NormalDifficultyOnly =
    [
        0,
        2,
        3
    ];

    public static IEnumerable<string> TargetDLLs { get; } =
    [
        "Assembly-CSharp.dll"
    ];

    public static void Patch(ref AssemblyDefinition assembly)
    {
        var rook = new CustomWildSpawnType(
            RookId,
            "bossRook",
            "Boss",
            KnightBrain,
            isBoss: true,
            isFollower: false,
            isHostileToEverybody: false);

        rook.SetCountAsBossForStatistics(true);
        rook.SetShouldUseFenceNoBossAttack(false, false);
        rook.SetExcludedDifficulties(NormalDifficultyOnly);
        CustomWildSpawnTypeManager.RegisterWildSpawnType(rook, assembly);

        var tombstone = new CustomWildSpawnType(
            TombstoneId,
            "followerTombstone",
            "Follower",
            BirdeyeBrain,
            isBoss: false,
            isFollower: true,
            isHostileToEverybody: false);

        tombstone.SetCountAsBossForStatistics(false);
        tombstone.SetShouldUseFenceNoBossAttack(false, false);
        tombstone.SetExcludedDifficulties(NormalDifficultyOnly);
        CustomWildSpawnTypeManager.RegisterWildSpawnType(tombstone, assembly);

        CustomWildSpawnTypeManager.AddSuitableGroup([RookId, TombstoneId]);
    }
}
