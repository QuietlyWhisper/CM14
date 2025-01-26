using System.Diagnostics.CodeAnalysis;
using System.Numerics;
using Content.Shared._RMC14.Map;
using Content.Shared._RMC14.Xenonids.Hive;
using Content.Shared._RMC14.Xenonids.Parasite;
using Content.Shared._RMC14.Xenonids.Weeds;
using Content.Shared.ActionBlocker;
using Content.Shared.Coordinates;
using Content.Shared.DoAfter;
using Content.Shared.DragDrop;
using Content.Shared.Hands.Components;
using Content.Shared.Interaction;
using Content.Shared.Interaction.Events;
using Content.Shared.Inventory.Events;
using Content.Shared.Item;
using Content.Shared.Maps;
using Content.Shared.Mobs.Systems;
using Content.Shared.Movement.Events;
using Content.Shared.Movement.Pulling.Components;
using Content.Shared.Movement.Pulling.Events;
using Content.Shared.Movement.Pulling.Systems;
using Content.Shared.Physics;
using Content.Shared.Popups;
using Content.Shared.Standing;
using Content.Shared.Stunnable;
using Content.Shared.Throwing;
using Content.Shared.Wall;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Network;
using Robust.Shared.Physics.Events;
using Robust.Shared.Player;
using Robust.Shared.Timing;

namespace Content.Shared._RMC14.Xenonids.Construction.Nest;

public sealed class XenoNestSystem : EntitySystem
{
    [Dependency] private readonly ActionBlockerSystem _actionBlocker = default!;
    [Dependency] private readonly SharedDoAfterSystem _doAfter = default!;
    [Dependency] private readonly IMapManager _map = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly INetManager _net = default!;
    [Dependency] private readonly OccluderSystem _occluder = default!;
    [Dependency] private readonly SharedXenoHiveSystem _hive = default!;
    [Dependency] private readonly SharedXenoParasiteSystem _parasite = default!;
    [Dependency] private readonly ISharedPlayerManager _player = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly PullingSystem _pulling = default!;
    [Dependency] private readonly SharedRMCMapSystem _rmcMap = default!;
    [Dependency] private readonly StandingStateSystem _standing = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly TurfSystem _turf = default!;
    [Dependency] private readonly SharedXenoWeedsSystem _xenoWeeds = default!;

    private EntityQuery<OccluderComponent> _occluderQuery;
    private EntityQuery<XenoNestComponent> _xenoNestQuery;
    private EntityQuery<XenoNestSurfaceComponent> _xenoNestSurfaceQuery;
    private EntityQuery<XenoWeedableComponent> _xenoWeedableQuery;

    public override void Initialize()
    {
        base.Initialize();

        _occluderQuery = GetEntityQuery<OccluderComponent>();
        _xenoNestQuery = GetEntityQuery<XenoNestComponent>();
        _xenoNestSurfaceQuery = GetEntityQuery<XenoNestSurfaceComponent>();
        _xenoWeedableQuery = GetEntityQuery<XenoWeedableComponent>();

        SubscribeLocalEvent<XenoComponent, GetUsedEntityEvent>(OnXenoGetUsedEntity);

        SubscribeLocalEvent<XenoNestSurfaceComponent, InteractHandEvent>(OnSurfaceInteractHand);
        SubscribeLocalEvent<XenoNestSurfaceComponent, DoAfterAttemptEvent<XenoNestDoAfterEvent>>(OnSurfaceDoAfterAttempt);
        SubscribeLocalEvent<XenoNestSurfaceComponent, XenoNestDoAfterEvent>(OnNestSurfaceDoAfter);
        SubscribeLocalEvent<XenoNestSurfaceComponent, CanDropTargetEvent>(OnSurfaceCanDropTarget);
        SubscribeLocalEvent<XenoNestSurfaceComponent, DragDropTargetEvent>(OnSurfaceDragDropTarget);
        SubscribeLocalEvent<XenoNestSurfaceComponent, EntityTerminatingEvent>(OnSurfaceTerminating);

        SubscribeLocalEvent<XenoNestComponent, ComponentRemove>(OnNestRemove);
        SubscribeLocalEvent<XenoNestComponent, EntityTerminatingEvent>(OnNestTerminating);

        SubscribeLocalEvent<XenoNestableComponent, BeforeRangedInteractEvent>(OnNestableBeforeRangedInteract);

        SubscribeLocalEvent<XenoNestedComponent, ComponentStartup>(OnNestedAdd);
        SubscribeLocalEvent<XenoNestedComponent, ComponentRemove>(OnNestedRemove);
        SubscribeLocalEvent<XenoNestedComponent, PreventCollideEvent>(OnNestedPreventCollide);
        SubscribeLocalEvent<XenoNestedComponent, PullAttemptEvent>(OnNestedPullAttempt);
        SubscribeLocalEvent<XenoNestedComponent, InteractionAttemptEvent>(OnNestedInteractionAttempt);
        SubscribeLocalEvent<XenoNestedComponent, UpdateCanMoveEvent>(OnNestedCancel);
        SubscribeLocalEvent<XenoNestedComponent, UseAttemptEvent>(OnNestedCancel);
        SubscribeLocalEvent<XenoNestedComponent, ThrowAttemptEvent>(OnNestedCancel);
        SubscribeLocalEvent<XenoNestedComponent, PickupAttemptEvent>(OnNestedCancel);
        SubscribeLocalEvent<XenoNestedComponent, AttackAttemptEvent>(OnNestedCancel);
        SubscribeLocalEvent<XenoNestedComponent, ChangeDirectionAttemptEvent>(OnNestedCancel);
        SubscribeLocalEvent<XenoNestedComponent, DownAttemptEvent>(OnNestedCancel);
        SubscribeLocalEvent<XenoNestedComponent, IsEquippingAttemptEvent>(OnNestedCancel);
        SubscribeLocalEvent<XenoNestedComponent, IsUnequippingAttemptEvent>(OnNestedCancel);
        SubscribeLocalEvent<XenoNestedComponent, GetInfectedIncubationMultiplierEvent>(OnInNestGetInfectedIncubationMultiplier);
    }

    private void OnXenoGetUsedEntity(Entity<XenoComponent> ent, ref GetUsedEntityEvent args)
    {
        if (args.Handled ||
            CompOrNull<PullerComponent>(ent)?.Pulling is not { } pulling ||
            !HasComp<XenoNestableComponent>(pulling) ||
            HasComp<XenoNestedComponent>(pulling))
        {
            return;
        }

        args.Used = pulling;
    }

    private void OnSurfaceInteractHand(Entity<XenoNestSurfaceComponent> ent, ref InteractHandEvent args)
    {
        if (CompOrNull<PullerComponent>(args.User)?.Pulling is not { } pulling)
            return;

        args.Handled = true;
        TryStartNesting(args.User, ent, pulling);
    }

    private void OnNestRemove(Entity<XenoNestComponent> ent, ref ComponentRemove args)
    {
        DetachNested(ent, ent.Comp.Nested);
    }

    private void OnNestTerminating(Entity<XenoNestComponent> ent, ref EntityTerminatingEvent args)
    {
        DetachNested(ent, ent.Comp.Nested);
    }

    private void OnNestableBeforeRangedInteract(Entity<XenoNestableComponent> ent, ref BeforeRangedInteractEvent args)
    {
        if (!args.CanReach || !TryComp(args.Target, out XenoNestSurfaceComponent? surface))
            return;

        args.Handled = true;
        TryStartNesting(args.User, (args.Target.Value, surface), args.Used);
    }

    private void OnNestedAdd(Entity<XenoNestedComponent> ent, ref ComponentStartup args)
    {
        _parasite.RefreshIncubationMultipliers(ent.Owner);
    }

    private void OnNestedRemove(Entity<XenoNestedComponent> ent, ref ComponentRemove args)
    {
        DetachNested(null, ent);
        _actionBlocker.UpdateCanMove(ent);

        _parasite.RefreshIncubationMultipliers(ent.Owner);

        // TODO RMC14
        if (HasComp<KnockedDownComponent>(ent) || _mobState.IsIncapacitated(ent))
            _standing.Down(ent, changeCollision: true);
    }

    private void OnSurfaceDoAfterAttempt(Entity<XenoNestSurfaceComponent> ent, ref DoAfterAttemptEvent<XenoNestDoAfterEvent> args)
    {
        if (args.DoAfter.Args.Target is not { } target ||
            TerminatingOrDeleted(target) ||
            !CanNestPopup(args.DoAfter.Args.User, target, ent, out _))
        {
            args.Cancel();
        }
    }

    private void OnNestSurfaceDoAfter(Entity<XenoNestSurfaceComponent> ent, ref XenoNestDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled)
            return;

        if (args.Target is not { } victim ||
            !CanNestPopup(args.User, victim, ent, out var direction))
        {
            return;
        }

        args.Handled = true;

        if (TryComp(victim, out PullableComponent? pullable))
            _pulling.TryStopPull(victim, pullable);

        if (_net.IsClient)
            return;

        var nestCoordinates = ent.Owner.ToCoordinates();
        var offset = direction switch
        {
            Direction.South => new Vector2(0, -0.25f),
            Direction.East => new Vector2(0.5f, 0),
            Direction.North => new Vector2(0, 0.5f),
            Direction.West => new Vector2(-0.5f, 0),
            _ => Vector2.Zero,
        };

        var nest = SpawnAttachedTo(ent.Comp.Nest, nestCoordinates);
        _transform.SetCoordinates(nest, nestCoordinates.Offset(offset));

        _hive.SetSameHive(args.User, nest);

        ent.Comp.Nests[direction.Value] = nest;
        Dirty(ent);

        var nestComp = EnsureComp<XenoNestComponent>(nest);
        nestComp.Surface = ent;
        nestComp.Nested = victim;
        Dirty(nest, nestComp);

        var nestedComp = EnsureComp<XenoNestedComponent>(victim);
        nestedComp.Nest = nest;
        Dirty(victim, nestedComp);

        _transform.SetCoordinates(victim, nest.ToCoordinates());
        _transform.SetLocalRotation(victim, direction.Value.ToAngle());

        _standing.Stand(victim, force: true);

        // TODO RMC14 make a method to do this
        _popup.PopupClient(Loc.GetString("cm-xeno-nest-securing-self", ("target", victim)), args.User, args.User);

        foreach (var session in Filter.PvsExcept(args.User).Recipients)
        {
            if (session.AttachedEntity is not { } recipient)
                continue;

            if (recipient == victim)
            {
                _popup.PopupEntity(Loc.GetString("cm-xeno-nest-securing-target", ("user", args.User)), args.User, recipient, PopupType.MediumCaution);
            }
            else
            {
                _popup.PopupEntity(Loc.GetString("cm-xeno-nest-securing-observer", ("user", args.User), ("target", victim)), args.User, recipient);
            }
        }
    }

    #region DragDrop

    private void OnSurfaceCanDropTarget(Entity<XenoNestSurfaceComponent> ent, ref CanDropTargetEvent args)
    {
        if (args.Handled)
            return;

        args.CanDrop = CanBeNested(args.User, args.Dragged, (ent, ent.Comp), out _, true);
        args.Handled = true;
    }

    private void OnSurfaceDragDropTarget(Entity<XenoNestSurfaceComponent> ent, ref DragDropTargetEvent args)
    {
        args.Handled = true;
        TryStartNesting(args.User, ent, args.Dragged);
    }

    private void OnSurfaceTerminating(Entity<XenoNestSurfaceComponent> ent, ref EntityTerminatingEvent args)
    {
        if (!TerminatingOrDeleted(ent.Comp.Weedable) &&
            _xenoWeedableQuery.TryComp(ent.Comp.Weedable, out var weedable) &&
            weedable.Entity == ent)
        {
            weedable.Entity = null;
            Dirty(ent.Comp.Weedable.Value, weedable);
        }
    }

    #endregion

    private void OnNestedPreventCollide(Entity<XenoNestedComponent> ent, ref PreventCollideEvent args)
    {
        args.Cancelled = true;
    }

    private void OnNestedPullAttempt(Entity<XenoNestedComponent> ent, ref PullAttemptEvent args)
    {
        args.Cancelled = true;
    }

    private void OnNestedInteractionAttempt(Entity<XenoNestedComponent> ent, ref InteractionAttemptEvent args)
    {
        args.Cancelled = true;
    }

    private void OnNestedCancel<T>(Entity<XenoNestedComponent> ent, ref T args) where T : CancellableEntityEventArgs
    {
        args.Cancel();
    }

    private void OnInNestGetInfectedIncubationMultiplier(Entity<XenoNestedComponent> ent, ref GetInfectedIncubationMultiplierEvent args)
    {
        if (ent.Comp.Running)
            args.Multiply(ent.Comp.IncubationMultiplier);
    }

    private void TryStartNesting(EntityUid user, Entity<XenoNestSurfaceComponent> surface, EntityUid victim)
    {
        if (!HasComp<XenoComponent>(user) || !HasComp<HandsComponent>(user) ||
            !CanNestPopup(user, victim, surface, out _))
        {
            return;
        }

        var ev = new XenoNestDoAfterEvent();
        var doAfter = new DoAfterArgs(EntityManager, user, surface.Comp.DoAfter, ev, surface, victim)
        {
            BreakOnMove = true,
            AttemptFrequency = AttemptFrequency.EveryTick
        };

        _doAfter.TryStartDoAfter(doAfter);

        // TODO RMC14 make a method to do this
        _popup.PopupClient(Loc.GetString("cm-xeno-nest-pin-self", ("target", victim)), user, user);

        foreach (var session in Filter.PvsExcept(user).Recipients)
        {
            if (session.AttachedEntity is not { } recipient)
                continue;

            if (recipient == victim)
            {
                _popup.PopupEntity(Loc.GetString("cm-xeno-nest-pin-target", ("user", user)), user, recipient, PopupType.MediumCaution);
            }
            else
            {
                _popup.PopupEntity(Loc.GetString("cm-xeno-nest-pin-observer", ("user", user), ("target", victim)), user, recipient);
            }
        }
    }

    private bool CanBeNested(EntityUid user,
        EntityUid victim,
        Entity<XenoNestSurfaceComponent?> surface,
        out List<Direction> directions,
        bool silent = false)
    {
        directions = new List<Direction>();

        if (!HasComp<XenoNestableComponent>(victim))
        {
            if (!silent)
                _popup.PopupClient(Loc.GetString("cm-xeno-nest-failed", ("target", victim)), surface, user);

            return false;
        }

        if (_mobState.IsDead(victim))
        {
            if (!silent)
                _popup.PopupClient(Loc.GetString("rmc-xeno-nest-failed-dead", ("target", victim)), surface, user);

            return false;
        }

        // Check cardinal face or two cardinals when diagonal
        var userCoords = _transform.GetMoverCoordinates(user);
        var nestCoords = _transform.GetMoverCoordinates(surface);
        if (!userCoords.TryDelta(EntityManager, _transform, nestCoords, out var delta))
            return false;

        var attemptedDirection = delta.GetDir();
        var priorityDir = Angle.FromWorldVec(delta).GetCardinalDir();
        if (attemptedDirection is Direction.Invalid)
            return false;

        // Add priority first
        directions.Add(priorityDir);

        var flags = attemptedDirection.AsFlag();
        // If directions doesn't contain the direction and flag is set, add it.
        if (!directions.Contains(Direction.South) && flags.HasFlag(DirectionFlag.South))
            directions.Add(Direction.South);
        if (!directions.Contains(Direction.East) && flags.HasFlag(DirectionFlag.East))
            directions.Add(Direction.East);
        if (!directions.Contains(Direction.North) && flags.HasFlag(DirectionFlag.North))
            directions.Add(Direction.North);
        if (!directions.Contains(Direction.West) && flags.HasFlag(DirectionFlag.West))
            directions.Add(Direction.West);

        if (!Resolve(surface, ref surface.Comp) || !IsNestSurfaceFromHiveWeeds((surface.Owner, surface.Comp), priorityDir))
        {
            if (!silent)
                _popup.PopupClient(Loc.GetString("cm-xeno-nest-failed-cant-there"), surface, user);

            return false;
        }

        return true;
    }


    private bool CanNestPopup(EntityUid user,
        EntityUid victim,
        Entity<XenoNestSurfaceComponent> surface,
        [NotNullWhen(true)] out Direction? direction,
        bool silent = false)
    {
        direction = null;

        if (!CanBeNested(user, victim, (surface, surface.Comp), out var directions, silent))
            return false;

        if (!_standing.IsDown(victim))
        {
            if (!silent)
                _popup.PopupClient(Loc.GetString("cm-xeno-nest-failed-target-resisting", ("target", victim)), victim, user, PopupType.MediumCaution);

            return false;
        }

        var nestCoords = _transform.GetMoverCoordinates(surface);

        string? response = null;
        foreach (var dir in directions)
        {
            if (nestCoords.Offset(dir.ToVec()).GetTileRef(EntityManager, _map) is not { } tile ||
                _turf.IsTileBlocked(tile, CollisionGroup.Impassable))
            {
                response ??= Loc.GetString("cm-xeno-nest-failed-cant-there");
                continue;
            }

            if (surface.Comp.Nests.ContainsKey(dir))
            {
                response ??= Loc.GetString("cm-xeno-nest-failed-cant-already-there");
                continue;
            }

            response = null;
            direction = dir;
            break;
        }

        if (direction == null)
        {
            if (!silent)
                _popup.PopupClient(response, surface, user);

            return false;
        }

        return true;
    }

    private bool IsNestSurfaceFromHiveWeeds(Entity<XenoNestSurfaceComponent> nestSurface, Direction dir)
    {
        var (ent, comp) = nestSurface;

        // Check if the weeds under this is a hive weed. (Resin wall)
        if (comp.Weedable is null)
        {
            var grid = _transform.GetGrid(ent);
            if (grid is null || !TryComp(grid, out MapGridComponent? gridComp))
            {
                return false;
            }

            return _xenoWeeds.IsOnHiveWeeds((grid.Value, gridComp), ent.ToCoordinates());
        }
        // Check if the weeds in the direction of this is a hive weed. (Weed wall)
        else
        {
            var nestCoords = _transform.GetMoverCoordinates(nestSurface);

            var underNestCooords = nestCoords.Offset(dir.ToVec());

            if (_transform.GetGrid(underNestCooords) is not EntityUid gridEntity ||
                !TryComp<MapGridComponent>(gridEntity, out var gridComp))
            {
                return false;
            }
            return _xenoWeeds.IsOnHiveWeeds((gridEntity, gridComp), underNestCooords);
        }

    }
    private bool NestAssoicatedWithHive()
    {
        return false;
    }

    private void DetachNested(EntityUid? nest, EntityUid? nestedNullable)
    {
        if (_timing.ApplyingState)
            return;

        if (nestedNullable is not { } nested ||
            TerminatingOrDeleted(nested) ||
            !TryComp(nested, out TransformComponent? xform))
        {
            return;
        }

        if (TryComp(nested, out XenoNestedComponent? nestedComp))
        {
            nest ??= nestedComp.Nest;

            if (nestedComp.Detached)
                return;

            nestedComp.Detached = true;
            Dirty(nested, nestedComp);

            if (TryComp(nest, out XenoNestComponent? nestComp) &&
                TryComp(nestComp.Surface, out XenoNestSurfaceComponent? surfaceComp))
            {
                foreach (var (dir, nestUid) in surfaceComp.Nests)
                {
                    if (nestUid != nest)
                        continue;

                    surfaceComp.Nests.Remove(dir);
                    break;
                }

                Dirty(nestComp.Surface.Value, surfaceComp);
            }
        }

        var position = xform.LocalPosition;
        _transform.SetLocalPosition(nested, position + xform.LocalRotation.ToWorldVec() / 2);
        _transform.AttachToGridOrMap(nested, xform);

        RemCompDeferred<XenoNestedComponent>(nested);
        QueueDel(nest);
    }

    private bool TryGetNestedWallOccluder(Entity<XenoNestedComponent> nested, out Entity<OccluderComponent> occluder)
    {
        occluder = default;
        if (!_xenoNestQuery.TryComp(nested.Comp.Nest, out var nest))
            return false;

        if (nest.Surface is not { } surface)
            return false;

        if (_occluderQuery.TryComp(surface, out var occluderComp))
        {
            occluder = (surface, occluderComp);
            return true;
        }

        if (_xenoNestSurfaceQuery.TryComp(surface, out var nestSurface) &&
            _occluderQuery.TryComp(nestSurface.Weedable, out occluderComp))
        {
            occluder = (nestSurface.Weedable.Value, occluderComp);
            return true;
        }

        return false;
    }

    public bool HasAdjacentNestFacing(EntityCoordinates coordinates)
    {
        foreach (var cardinal in _rmcMap.CardinalDirections)
        {
            var anchored = _rmcMap.GetAnchoredEntitiesEnumerator(coordinates, cardinal);
            var opposite = cardinal.GetOpposite();
            while (anchored.MoveNext(out var uid))
            {
                if (!_xenoNestSurfaceQuery.TryComp(uid, out var surface))
                    continue;

                if (surface.Nests.TryGetValue(opposite, out var nest) &&
                    !TerminatingOrDeleted(nest))
                {
                    return true;
                }
            }
        }

        return false;
    }

    public override void Update(float frameTime)
    {
        if (_net.IsServer)
            return;

        if (_player.LocalEntity is not { } ent)
            return;

        if (TryComp(ent, out XenoNestedComponent? nested) &&
            TryGetNestedWallOccluder((ent, nested), out var occluder))
        {
            _occluder.SetEnabled(occluder, false, occluder);
        }
    }
}
