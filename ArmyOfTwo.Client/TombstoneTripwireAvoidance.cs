using System.Collections.Generic;
using Comfort.Common;
using EFT;
using EFT.SynchronizableObjects;
using UnityEngine;

namespace ArmyOfTwo.Client;

internal sealed class TombstoneTripwireAvoidance : MonoBehaviour
{
    private sealed class Wire
    {
        internal int Id;
        internal Vector3 From;
        internal Vector3 To;
        internal Vector3 Center;
        internal string PlacerId;
    }

    private static readonly List<Wire> Wires = new List<Wire>();
    private static int _nextWireId;

    private readonly HashSet<int> _reportedFailures = new HashSet<int>();
    private BotOwner _bot;
    private float _nextCheckAt;

    internal static void Reset()
    {
        Wires.Clear();
        _nextWireId = 0;
    }

    internal static void Register(Vector3 from, Vector3 to, string placerId)
    {
        Wires.Add(new Wire
        {
            Id = ++_nextWireId,
            From = from,
            To = to,
            Center = (from + to) * 0.5f,
            PlacerId = placerId
        });
    }

    internal static bool IsNearKnownWire(Vector3 point, float distance)
    {
        foreach (var wire in Wires)
        {
            if (DistanceToSegment(point, wire.From, wire.To) < distance)
            {
                return true;
            }
        }

        return false;
    }

    internal static bool IsRouteNearKnownWire(Vector3 from, Vector3 to, float distance)
    {
        foreach (var wire in Wires)
        {
            if (DistanceToSegment(wire.Center, from, to) < distance ||
                DistanceToSegment(wire.From, from, to) < distance ||
                DistanceToSegment(wire.To, from, to) < distance)
            {
                return true;
            }
        }

        return false;
    }

    internal void Init(BotOwner bot)
    {
        _bot = bot;
    }

    private void LateUpdate()
    {
        if (_bot == null || _bot.IsDead || Wires.Count == 0 ||
            Time.time < _nextCheckAt || !_bot.HasPathAndNotComplete || _bot.Mover == null)
        {
            return;
        }

        _nextCheckAt = Time.time + 0.5f;
        foreach (var wire in Wires)
        {
            var difference = _bot.Position - wire.Center;
            difference.y = 0f;
            if (difference.sqrMagnitude > 25f * 25f ||
                !_bot.Mover.IsPointOnCurrentWay(wire.Center, 1.5f))
            {
                continue;
            }

            var towardCorner = _bot.Mover.CurrentCornerPoint - _bot.Position;
            towardCorner.y = 0f;
            if (difference.sqrMagnitude < 8f * 8f &&
                Vector3.Dot(towardCorner, -difference) > 0f &&
                TryDeactivateWire(wire))
            {
                Wires.Remove(wire);
                Plugin.Log?.LogWarning(
                    "An Army of Two bot approached Tombstone's tripwire, so it was deactivated before contact.");
                return;
            }

            var dangerPoints = new List<Vector3> { wire.From, wire.Center, wire.To };
            if (_bot.Mover.TryRelacePathAround(wire.Center, dangerPoints, out _))
            {
                _reportedFailures.Remove(wire.Id);
                return;
            }

            if (TryDeactivateWire(wire))
            {
                Wires.Remove(wire);
                Plugin.Log?.LogWarning(
                    "A Tombstone tripwire was deactivated because an Army of Two bot could not route around it.");
                return;
            }

            if (_reportedFailures.Add(wire.Id))
            {
                Plugin.Log?.LogWarning(
                    $"Could not reroute {_bot.Profile?.Info?.Nickname ?? "Army of Two bot"} around Tombstone's tripwire.");
            }
        }
    }

    private static bool TryDeactivateWire(Wire wire)
    {
        if (!Singleton<GameWorld>.Instantiated)
        {
            return false;
        }

        var world = Singleton<GameWorld>.Instance;
        var tripwires = world.SynchronizableObjectLogicProcessor?.TripwireManager?._tripwires;
        if (tripwires == null)
        {
            return false;
        }

        foreach (var tripwire in tripwires)
        {
            if (tripwire == null || tripwire.PlacerPlayerId.ToString() != wire.PlacerId)
            {
                continue;
            }

            var sameDirection = Vector3.Distance(tripwire.FromPosition, wire.From) < 0.75f &&
                Vector3.Distance(tripwire.ToPosition, wire.To) < 0.75f;
            var reversed = Vector3.Distance(tripwire.FromPosition, wire.To) < 0.75f &&
                Vector3.Distance(tripwire.ToPosition, wire.From) < 0.75f;
            if (!sameDirection && !reversed)
            {
                continue;
            }

            world.DeActivateTripwire(tripwire);
            return true;
        }

        return false;
    }

    private static float DistanceToSegment(Vector3 point, Vector3 from, Vector3 to)
    {
        point.y = 0f;
        from.y = 0f;
        to.y = 0f;
        var segment = to - from;
        if (segment.sqrMagnitude < 0.01f)
        {
            return Vector3.Distance(point, from);
        }

        var progress = Mathf.Clamp01(Vector3.Dot(point - from, segment) / segment.sqrMagnitude);
        return Vector3.Distance(point, from + segment * progress);
    }
}
