using System.Reflection;
using SPTarkov.DI.Annotations;
using SPTarkov.Reflection.Patching;
using SPTarkov.Server.Core.Generators.Bot;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;
using SPTarkov.Server.Core.Models.Enums;
using SPTarkov.Server.Core.Models.Spt.Bots;

namespace ArmyOfTwo.Server.Loadouts;

[Injectable(InjectionType.Singleton)]
public sealed class ReserveAmmoPatch : AbstractPatch
{
    private const string RookAmmoTpl = "59e6906286f7746c9f75e847";
    private const string TombstoneAmmoTpl = "58dd3ad986f77403051cba8f";
    private bool _enabled;

    public ReserveAmmoPatch() : base(nameof(ReserveAmmoPatch))
    {
    }

    internal void Start()
    {
        if (_enabled)
        {
            return;
        }

        Enable();
        _enabled = true;
    }

    protected override MethodBase? GetTargetMethod()
    {
        return typeof(BotInventoryGenerator).GetMethod(
            "GenerateInventory",
            BindingFlags.Instance | BindingFlags.Public,
            null,
            [typeof(MongoId), typeof(MongoId), typeof(BotType), typeof(BotGenerationDetails)],
            null);
    }

    [PatchPostfix]
    private static void SetReserveAmmo(BotBaseInventory __result, BotGenerationDetails botGenerationDetails)
    {
        var role = botGenerationDetails.RoleLowercase;
        var reserve = role switch
        {
            "bossrook" => (AmmoTpl: RookAmmoTpl, StackSize: 60),
            "followertombstone" => (AmmoTpl: TombstoneAmmoTpl, StackSize: 40),
            _ => (AmmoTpl: string.Empty, StackSize: 0)
        };

        if (reserve.StackSize == 0)
        {
            return;
        }

        var items = __result.Items;
        if (items is null)
        {
            return;
        }

        var secureContainer = items.FirstOrDefault(item =>
            string.Equals(item.SlotId, EquipmentSlots.SecuredContainer.ToString(), StringComparison.OrdinalIgnoreCase));
        if (secureContainer is null)
        {
            return;
        }

        var reserveStacks = items
            .Where(item => item.ParentId == secureContainer.Id.ToString() && item.Template.ToString() == reserve.AmmoTpl)
            .ToList();

        foreach (var reserveStack in reserveStacks.Take(2))
        {
            reserveStack.Upd ??= new Upd();
            reserveStack.Upd.StackObjectsCount = reserve.StackSize;
        }

        foreach (var extraStack in reserveStacks.Skip(2))
        {
            items.Remove(extraStack);
        }
    }
}
