using System;
using System.Linq;
using Comfort.Common;
using EFT;
using EFT.InventoryLogic;
using EFT.SynchronizableObjects;
using UnityEngine;
using UnityEngine.AI;

namespace ArmyOfTwo.Client;

internal static class TombstoneTripwireEquipment
{
    private const string TripwireGrenade = "67b49e7335dec48e3e05e057";
    private const string InstallationKit = "666b11055a706400b717cfa5";
    private static readonly float[] SearchDistances = { 3f, 5f, 7f };
    private static readonly float[] SearchAngles = { 180f, -135f, 135f, -90f, 90f, -45f, 45f, 0f };
    private static readonly RaycastHit[] VisibilityHits = new RaycastHit[12];

    internal static bool EnsureOneSet(BotOwner bot)
    {
        CountSupplies(bot, out var grenades, out var kits);
        while (grenades < 1 || kits < 1)
        {
            var template = kits <= grenades ? InstallationKit : TripwireGrenade;
            if (!TryAddToSecureContainer(bot, template))
            {
                return false;
            }

            if (template == InstallationKit)
            {
                kits++;
            }
            else
            {
                grenades++;
            }
        }

        return true;
    }

    internal static bool TryGetSupplies(BotOwner bot, out ThrowWeap grenade, out PlantingKit kit)
    {
        var items = bot?.GetPlayer?.InventoryController?.Inventory?.GetPlayerItems(EPlayerItems.Equipment);
        grenade = items?.OfType<ThrowWeap>().FirstOrDefault(item =>
            string.Equals(item.StringTemplateId, TripwireGrenade, StringComparison.OrdinalIgnoreCase) &&
            item.CanPlantOnGround);
        kit = items?.OfType<PlantingKit>().FirstOrDefault(item =>
            string.Equals(item.StringTemplateId, InstallationKit, StringComparison.OrdinalIgnoreCase));
        return grenade != null && kit != null;
    }

    internal static bool IsSafe(BotOwner bot, out string reason)
    {
        reason = null;
        if (bot == null || bot.Memory?.GoalEnemy?.IsVisible == true ||
            bot.Memory?.IsUnderFire == true || bot.ShootData?.Shooting == true)
        {
            reason = "combat or a visible enemy nearby";
            return false;
        }

        if (!Singleton<GameWorld>.Instantiated)
        {
            reason = "game world unavailable";
            return false;
        }

        var botEye = bot.Position + Vector3.up * 1.55f;
        foreach (var player in Singleton<GameWorld>.Instance.AllAlivePlayersList)
        {
            if (player == null || player == bot.GetPlayer)
            {
                continue;
            }

            var difference = player.Position - bot.Position;
            difference.y = 0f;
            if (player.IsAI) continue;

            if (difference.sqrMagnitude < 12f * 12f ||
                (difference.sqrMagnitude < 120f * 120f && HasLineOfSight(player, bot, botEye)))
            {
                reason = "a player is close to or can see Tombstone";
                return false;
            }
        }

        return true;
    }

    internal static bool TryFindPlacement(
        BotOwner bot,
        out Vector3 from,
        out Vector3 to,
        out string placementKind,
        out string reason)
    {
        from = Vector3.zero;
        to = Vector3.zero;
        placementKind = null;
        reason = "no safe tripwire position was found near Tombstone";

        if (TryFindRoutePlacement(bot, out from, out to))
        {
            placementKind = "travelled route";
            reason = null;
            return true;
        }

        var forward = GetSearchForward(bot);
        foreach (var distance in SearchDistances)
        {
            foreach (var angle in SearchAngles)
            {
                var direction = Quaternion.Euler(0f, angle, 0f) * forward;
                var midpoint = bot.Position + direction * distance;
                if (!TryBuildPlacement(bot, midpoint, direction, false, out from, out to))
                {
                    continue;
                }

                placementKind = "local fallback";
                reason = null;
                return true;
            }
        }

        return false;
    }

    private static bool TryFindRoutePlacement(BotOwner bot, out Vector3 from, out Vector3 to)
    {
        from = Vector3.zero;
        to = Vector3.zero;
        if (bot.Mover == null || !bot.HasPathAndNotComplete)
        {
            return false;
        }

        var previousCorner = bot.Mover.PrevCorner();
        var travel = bot.Position - previousCorner;
        travel.y = 0f;
        if (travel.sqrMagnitude < 2f * 2f)
        {
            return false;
        }

        var direction = travel.normalized;
        var distanceBehind = Mathf.Clamp(travel.magnitude * 0.5f, 1.5f, 5f);
        var midpoint = bot.Position - direction * distanceBehind;
        return TryBuildPlacement(bot, midpoint, direction, true, out from, out to);
    }

    private static bool TryBuildPlacement(
        BotOwner bot,
        Vector3 midpoint,
        Vector3 approachDirection,
        bool allowPreviousRoute,
        out Vector3 from,
        out Vector3 to)
    {
        from = Vector3.zero;
        to = Vector3.zero;
        approachDirection.y = 0f;
        if (approachDirection.sqrMagnitude < 0.01f)
        {
            return false;
        }

        approachDirection.Normalize();
        var across = new Vector3(-approachDirection.z, 0f, approachDirection.x) * 0.65f;
        if (!NavMesh.SamplePosition(midpoint + across, out var firstHit, 0.8f, NavMesh.AllAreas) ||
            !NavMesh.SamplePosition(midpoint - across, out var secondHit, 0.8f, NavMesh.AllAreas))
        {
            return false;
        }

        from = firstHit.position;
        to = secondHit.position;
        if (!TripwireSynchronizableObject.ValidateTripwirePosition(
                from, from, to, true, false, true))
        {
            return false;
        }

        var wireMidpoint = (from + to) * 0.5f;
        if (DistanceToSegment(bot.Position, from, to) < 2f)
        {
            return false;
        }

        if (!allowPreviousRoute && bot.Mover?.IsPointOnCurrentWay(wireMidpoint, 1.5f) == true)
        {
            return false;
        }

        if (IsAllyOnWire(bot, from, to) || IsGroupRouteOnWire(bot, (from + to) * 0.5f))
        {
            return false;
        }

        if (TombstoneTripwireAvoidance.IsNearKnownWire(wireMidpoint, 3f))
        {
            return false;
        }

        return true;
    }

    private static Vector3 GetSearchForward(BotOwner bot)
    {
        if (bot.Mover != null && bot.HasPathAndNotComplete)
        {
            var pathDirection = bot.Mover.CurrentCornerPoint - bot.Position;
            pathDirection.y = 0f;
            if (pathDirection.sqrMagnitude > 0.25f)
            {
                return pathDirection.normalized;
            }
        }

        var transform = bot.GetPlayer?.Transform?.Original;
        if (transform != null)
        {
            var lookDirection = transform.forward;
            lookDirection.y = 0f;
            if (lookDirection.sqrMagnitude > 0.01f)
            {
                return lookDirection.normalized;
            }
        }

        return Vector3.forward;
    }

    private static float DistanceToSegment(Vector3 point, Vector3 start, Vector3 end)
    {
        point.y = 0f;
        start.y = 0f;
        end.y = 0f;
        var segment = end - start;
        var lengthSquared = segment.sqrMagnitude;
        if (lengthSquared < 0.01f)
        {
            return Vector3.Distance(point, start);
        }

        var progress = Mathf.Clamp01(Vector3.Dot(point - start, segment) / lengthSquared);
        return Vector3.Distance(point, start + segment * progress);
    }

    private static bool IsAllyOnWire(BotOwner bot, Vector3 from, Vector3 to)
    {
        foreach (var player in Singleton<GameWorld>.Instance.AllAlivePlayersList)
        {
            if (player == null || player == bot.GetPlayer || !player.IsAI)
            {
                continue;
            }

            if (DistanceToSegment(player.Position, from, to) < 2.5f)
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsGroupRouteOnWire(BotOwner bot, Vector3 midpoint)
    {
        var members = bot.BotsGroup?._members;
        if (members == null)
        {
            return false;
        }

        foreach (var member in members)
        {
            if (member != null && member != bot && !member.IsDead &&
                member.Mover?.IsPointOnCurrentWay(midpoint, 1.5f) == true)
            {
                return true;
            }
        }

        return false;
    }

    private static void CountSupplies(BotOwner bot, out int grenades, out int kits)
    {
        grenades = 0;
        kits = 0;
        var items = bot?.GetPlayer?.InventoryController?.Inventory?.GetPlayerItems(EPlayerItems.Equipment);
        if (items == null)
        {
            return;
        }

        grenades = items.OfType<ThrowWeap>().Count(item =>
            string.Equals(item.StringTemplateId, TripwireGrenade, StringComparison.OrdinalIgnoreCase) &&
            item.CanPlantOnGround);
        kits = items.OfType<PlantingKit>().Count(item =>
            string.Equals(item.StringTemplateId, InstallationKit, StringComparison.OrdinalIgnoreCase));
    }

    private static bool TryAddToSecureContainer(BotOwner bot, string template)
    {
        var controller = bot?.GetPlayer?.InventoryController;
        var secureContainer = controller?.Inventory?.Equipment?
            .GetSlot(EquipmentSlot.SecuredContainer)?.ContainedItem as CompoundItem;
        var factory = Singleton<ItemFactory>.Instance;
        if (controller == null || secureContainer == null || factory == null)
        {
            return false;
        }

        var item = factory.CreateItem(controller.NextId.ToString(), template, null);
        if (item == null)
        {
            return false;
        }

        foreach (var grid in secureContainer.Grids)
        {
            var address = grid.FindLocationForItem(item);
            if (address == null)
            {
                continue;
            }

            var result = ItemManipulator.AddWithoutRestrictions(item, address, controller);
            if (!result.Succeeded)
            {
                continue;
            }

            result.Value.RaiseEvents(controller, CommandStatus.Begin);
            result.Value.RaiseEvents(controller, CommandStatus.Succeed);
            return true;
        }

        return false;
    }

    private static bool HasLineOfSight(Player player, BotOwner bot, Vector3 botEye)
    {
        var playerEye = player.Position + Vector3.up * 1.55f;
        var difference = botEye - playerEye;
        var distance = difference.magnitude;
        if (distance < 0.25f)
        {
            return true;
        }

        var hitCount = Physics.RaycastNonAlloc(
            playerEye,
            difference / distance,
            VisibilityHits,
            distance - 0.1f,
            LayersMaskController.HighPolyWithTerrainMaskAI,
            QueryTriggerInteraction.Ignore);
        var playerTransform = player.Transform?.Original;
        var botTransform = bot.GetPlayer?.Transform?.Original;
        for (var index = 0; index < hitCount; index++)
        {
            var collider = VisibilityHits[index].collider;
            if (collider == null)
            {
                continue;
            }

            var hitTransform = collider.transform;
            if ((playerTransform != null && hitTransform.IsChildOf(playerTransform)) ||
                (botTransform != null && hitTransform.IsChildOf(botTransform)))
            {
                continue;
            }

            return false;
        }

        return true;
    }
}
