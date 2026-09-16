using System.Reflection;
using SPTarkov.DI.Annotations;
using SPTarkov.Reflection.Patching;
using SPTarkov.Server.Core.Generators.Bot;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;
using SPTarkov.Server.Core.Models.Spt.Bots;

namespace ArmyOfTwo.Server.Loadouts;

[Injectable(InjectionType.Singleton)]
public sealed class ReserveAmmoPatch : AbstractPatch
{
    private const string Hk416 = "5bb2475ed4351e00853264e3";
    private const string M60 = "661cec09b2c6356b4d0c7a36";
    private const string Sr25 = "5df8ce05b11454561e39243b";
    private const string Axmc = "627e14b21713922ded6f2c15";
    private const string M856A1 = "59e6906286f7746c9f75e847";
    private const string M80 = "58dd3ad986f77403051cba8f";
    private const string Ucw = "5fc382c1016cce60e8341b20";
    private const string Pbp = "5efb0da7a29a85116f6ea05f";
    private const string Hk416Magazine = "544a378f4bdc2d30388b4567";
    private const string M60Magazine = "660ea4453786cc0af808a1be";
    private const string Sr25Magazine = "5a3501acc4a282000d72293a";
    private const string AxmcMagazine = "628120fd5631d45211793c9f";
    private const string GlockMagazine = "63076701a987397c0816d21b";
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
        var items = __result.Items;
        if (items is null)
        {
            return;
        }

        var role = botGenerationDetails.RoleLowercase;
        if (role != "bossrook" && role != "followertombstone")
        {
            return;
        }

        Item? primary = null;
        foreach (var item in items)
        {
            if (item.SlotId == "FirstPrimaryWeapon")
            {
                primary = item;
                break;
            }
        }

        var primaryTpl = primary?.Template.ToString();
        if (role == "bossrook")
        {
            if (primaryTpl == Hk416)
            {
                SetAmmoStackCount(items, M856A1, 60, 400);
                TrimSpareMagazines(items, Hk416Magazine, 3);
            }
            else if (primaryTpl == M60)
            {
                SetAmmoStackCount(items, M80, 40, 600);
                TrimSpareMagazines(items, M60Magazine, 2);
            }
        }
        else if (primaryTpl == Sr25)
        {
            SetAmmoStackCount(items, M80, 40, 220);
            TrimSpareMagazines(items, Sr25Magazine, 3);
        }
        else if (primaryTpl == Axmc)
        {
            SetAmmoStackCount(items, Ucw, 30, 100);
            TrimSpareMagazines(items, AxmcMagazine, 3);
        }

        SetAmmoStackCount(items, Pbp, 50, 60);
        TrimSpareMagazines(items, GlockMagazine, 2);
    }

    private static void SetAmmoStackCount(List<Item> items, string ammoTpl, int stackSize, int total)
    {
        Item? secureContainer = null;
        foreach (var item in items)
        {
            if (item.SlotId == "SecuredContainer")
            {
                secureContainer = item;
                break;
            }
        }

        if (secureContainer is null)
        {
            return;
        }

        var containerId = secureContainer.Id.ToString();
        var stacks = new List<Item>();
        foreach (var item in items)
        {
            if (item.ParentId == containerId && item.Template.ToString() == ammoTpl)
            {
                stacks.Add(item);
            }
        }

        var remaining = total;
        foreach (var stack in stacks)
        {
            if (remaining == 0)
            {
                items.Remove(stack);
                continue;
            }

            var amount = Math.Min(stackSize, remaining);
            stack.Upd ??= new Upd();
            stack.Upd.StackObjectsCount = amount;
            remaining -= amount;
        }
    }

    private static void TrimSpareMagazines(List<Item> items, string magazineTpl, int spareCount)
    {
        var spareMagazines = new List<Item>();
        foreach (var item in items)
        {
            if (item.Template.ToString() == magazineTpl && item.SlotId != "mod_magazine")
            {
                spareMagazines.Add(item);
            }
        }

        for (var i = spareCount; i < spareMagazines.Count; i++)
        {
            RemoveWithChildren(items, spareMagazines[i]);
        }
    }

    private static void RemoveWithChildren(List<Item> items, Item parent)
    {
        var parentId = parent.Id.ToString();
        var children = new List<Item>();
        foreach (var item in items)
        {
            if (item.ParentId == parentId)
            {
                children.Add(item);
            }
        }

        foreach (var child in children)
        {
            RemoveWithChildren(items, child);
        }

        items.Remove(parent);
    }
}
