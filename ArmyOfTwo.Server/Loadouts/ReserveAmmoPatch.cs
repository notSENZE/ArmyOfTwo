using System.Reflection;
using SPTarkov.DI.Annotations;
using SPTarkov.Reflection.Patching;
using SPTarkov.Server.Core.Generators.Bot;
using SPTarkov.Server.Core.Helpers.Bot;
using SPTarkov.Server.Core.Helpers.Items;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;
using SPTarkov.Server.Core.Models.Spt.Bots;

namespace ArmyOfTwo.Server.Loadouts;

[Injectable(InjectionType.Singleton)]
public sealed class ReserveAmmoPatch : AbstractPatch
{
    private const string Hk416 = "5bb2475ed4351e00853264e3";
    private const string M60 = "661cec09b2c6356b4d0c7a36";
    private const string Ak74M = "5ac4cd105acfc40016339859";
    private const string Sr25 = "5df8ce05b11454561e39243b";
    private const string Axmc = "627e14b21713922ded6f2c15";
    private const string Svds = "5c46fbd72e2216398b5a8c9c";
    private const string M856A1 = "59e6906286f7746c9f75e847";
    private const string M80 = "58dd3ad986f77403051cba8f";
    private const string Ucw = "5fc382c1016cce60e8341b20";
    private const string SevenN40 = "61962b617c6c7b169525f168";
    private const string Snb = "560d61e84bdc2da74d8b4571";
    private const string Pbp = "5efb0da7a29a85116f6ea05f";
    private const string Hk416Magazine = "544a378f4bdc2d30388b4567";
    private const string M60Magazine = "660ea4453786cc0af808a1be";
    private const string Ak74MMagazine = "5aaa4194e5b5b055d06310a5";
    private const string Sr25Magazine = "5a3501acc4a282000d72293a";
    private const string AxmcMagazine = "628120fd5631d45211793c9f";
    private const string SvdsMagazine = "5c88f24b2e22160bc12c69a6";
    private const string GlockMagazine = "63076701a987397c0816d21b";
    private const string DvlMount = "57c69dd424597774c03b7bbc";
    private const string Sr25Scope = "5b3b99475acfc432ff4dcbee";
    private const string SvdsScope = "57c5ac0824597754771e88a9";

    private static BotGeneratorHelper _botGeneratorHelper = null!;
    private static ItemHelper _itemHelper = null!;
    private bool _enabled;

    public ReserveAmmoPatch(
        BotGeneratorHelper botGeneratorHelper,
        ItemHelper itemHelper) : base(nameof(ReserveAmmoPatch))
    {
        _botGeneratorHelper = botGeneratorHelper;
        _itemHelper = itemHelper;
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
            else if (primaryTpl == Ak74M)
            {
                SetAmmoStackCount(items, SevenN40, 60, 300);
                TrimSpareMagazines(items, Ak74MMagazine, 4);
            }
        }
        else if (primaryTpl == Sr25)
        {
            SetAmmoStackCount(items, M80, 40, 220);
            TrimSpareMagazines(items, Sr25Magazine, 3);
            SetScope(items, primary!, DvlMount, Sr25Scope, role);
        }
        else if (primaryTpl == Axmc)
        {
            SetAmmoStackCount(items, Ucw, 30, 100);
            TrimSpareMagazines(items, AxmcMagazine, 3);
        }
        else if (primaryTpl == Svds)
        {
            SetAmmoStackCount(items, Snb, 40, 300);
            TrimSpareMagazines(items, SvdsMagazine, 4);
            SetScope(items, primary!, DvlMount, SvdsScope, role);
        }

        SetAmmoStackCount(items, Pbp, 50, 60);
        TrimSpareMagazines(items, GlockMagazine, 2);
    }

    private static void SetScope(
        List<Item> items,
        Item primary,
        string mountTpl,
        string scopeTpl,
        string role)
    {
        var mount = items.FirstOrDefault(item =>
            item.Template.ToString() == mountTpl
            && IsChildOf(items, item, primary.Id.ToString()));
        if (mount is null)
        {
            return;
        }

        var oldScopes = items
            .Where(item => item.ParentId == mount.Id.ToString() && item.SlotId == "mod_scope")
            .ToList();
        foreach (var oldScope in oldScopes)
        {
            RemoveWithChildren(items, oldScope);
        }

        var template = _itemHelper.GetItem(new MongoId(scopeTpl));
        if (!template.Key)
        {
            return;
        }

        items.Add(new Item
        {
            Id = new MongoId(),
            Template = new MongoId(scopeTpl),
            ParentId = mount.Id.ToString(),
            SlotId = "mod_scope",
            Upd = _botGeneratorHelper.GenerateExtraPropertiesForItem(template.Value, role, true)
        });
    }

    private static bool IsChildOf(List<Item> items, Item item, string rootId)
    {
        var parentId = item.ParentId;
        while (!string.IsNullOrWhiteSpace(parentId))
        {
            if (parentId == rootId)
            {
                return true;
            }

            var parent = items.FirstOrDefault(candidate => candidate.Id.ToString() == parentId);
            if (parent is null)
            {
                return false;
            }

            parentId = parent.ParentId;
        }

        return false;
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
