using System.Reflection;
using SPTarkov.DI.Annotations;
using SPTarkov.Reflection.Patching;
using SPTarkov.Server.Core.Generators.Loot;
using SPTarkov.Server.Core.Helpers.Bot;
using SPTarkov.Server.Core.Helpers.Items;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;
using SPTarkov.Server.Core.Models.Enums;
using SPTarkov.Server.Core.Models.Spt.Bots;
using SPTarkov.Server.Core.Services.Bot;

namespace ArmyOfTwo.Server.Loadouts;

[Injectable(InjectionType.Singleton)]
public sealed class MedicalLoadoutPatch : AbstractPatch
{
    private const string Surv12 = "5d02797c86f774203f38e30a";
    private const string CalokB = "5e8488fa988a8701445df1e4";
    private const string Afak = "60098ad7c2240c0fe85c570a";

    private static BotInventoryContainerService _containers = null!;
    private static BotGeneratorHelper _botGeneratorHelper = null!;
    private static ItemHelper _itemHelper = null!;
    private bool _enabled;

    public MedicalLoadoutPatch(
        BotInventoryContainerService containers,
        BotGeneratorHelper botGeneratorHelper,
        ItemHelper itemHelper) : base(nameof(MedicalLoadoutPatch))
    {
        _containers = containers;
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
        return typeof(BotLootGenerator).GetMethod(
            "GenerateLoot",
            BindingFlags.Instance | BindingFlags.Public,
            null,
            [typeof(MongoId), typeof(MongoId), typeof(BotType), typeof(BotGenerationDetails), typeof(BotBaseInventory)],
            null);
    }

    [PatchPrefix]
    private static void AddMedicalSupplies(MongoId botId, BotGenerationDetails botGenerationDetails, BotBaseInventory botInventory)
    {
        var role = botGenerationDetails.RoleLowercase;
        if (role != "bossrook" && role != "followertombstone")
        {
            return;
        }

        AddItem(botId, botInventory, role, Surv12, 3, 1, EquipmentSlots.SecuredContainer);
        AddItem(botId, botInventory, role, CalokB, 1, 1, EquipmentSlots.TacticalVest, EquipmentSlots.Pockets, EquipmentSlots.SecuredContainer);
        AddItem(botId, botInventory, role, CalokB, 1, 1, EquipmentSlots.TacticalVest, EquipmentSlots.Pockets, EquipmentSlots.SecuredContainer);
        AddItem(botId, botInventory, role, Afak, 1, 1, EquipmentSlots.TacticalVest, EquipmentSlots.SecuredContainer);
    }

    private static void AddItem(
        MongoId botId,
        BotBaseInventory inventory,
        string role,
        string templateId,
        int width,
        int height,
        params EquipmentSlots[] slots)
    {
        var template = _itemHelper.GetItem(new MongoId(templateId));
        if (!template.Key)
        {
            return;
        }

        var item = new Item
        {
            Id = new MongoId(),
            Template = new MongoId(templateId),
            Upd = _botGeneratorHelper.GenerateExtraPropertiesForItem(template.Value, role, true)
        };

        foreach (var slot in slots)
        {
            var result = _containers.TryAddItemToBotContainer(botId, slot, [item], inventory, width, height);
            if (result == ItemAddedResult.SUCCESS)
            {
                return;
            }
        }
    }
}
