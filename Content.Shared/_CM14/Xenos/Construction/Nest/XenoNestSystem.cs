﻿using System.Numerics;
using Content.Shared.Coordinates;
using Content.Shared.DoAfter;
using Content.Shared.Interaction;
using Content.Shared.Interaction.Events;
using Content.Shared.Item;
using Content.Shared.Movement.Events;
using Content.Shared.Movement.Pulling.Components;
using Content.Shared.Movement.Pulling.Events;
using Content.Shared.Movement.Pulling.Systems;
using Content.Shared.Throwing;
using Robust.Shared.Network;
using Robust.Shared.Physics.Events;
using Robust.Shared.Timing;

namespace Content.Shared._CM14.Xenos.Construction.Nest;

public sealed class XenoNestSystem : EntitySystem
{
    [Dependency] private readonly SharedDoAfterSystem _doAfter = default!;
    [Dependency] private readonly INetManager _net = default!;
    [Dependency] private readonly PullingSystem _pulling = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;

    private readonly List<Direction> _candidateNests = new();

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<XenoNestCandidateComponent, InteractHandEvent>(OnNestCandidateInteractHand);
        SubscribeLocalEvent<XenoNestCandidateComponent, XenoNestDoAfterEvent>(OnNestCandidateDoAfter);

        SubscribeLocalEvent<XenoNestComponent, ComponentRemove>(OnNestRemove);
        SubscribeLocalEvent<XenoNestComponent, EntityTerminatingEvent>(OnNestTerminating);

        SubscribeLocalEvent<XenoNestedComponent, ComponentRemove>(OnNestedRemove);
        SubscribeLocalEvent<XenoNestedComponent, PreventCollideEvent>(OnNestedPreventCollide);
        SubscribeLocalEvent<XenoNestedComponent, PullAttemptEvent>(OnNestedPullAttempt);
        SubscribeLocalEvent<XenoNestedComponent, UpdateCanMoveEvent>(OnNestedCancel);
        SubscribeLocalEvent<XenoNestedComponent, InteractionAttemptEvent>(OnNestedCancel);
        SubscribeLocalEvent<XenoNestedComponent, UseAttemptEvent>(OnNestedCancel);
        SubscribeLocalEvent<XenoNestedComponent, ThrowAttemptEvent>(OnNestedCancel);
        SubscribeLocalEvent<XenoNestedComponent, PickupAttemptEvent>(OnNestedCancel);
        SubscribeLocalEvent<XenoNestedComponent, AttackAttemptEvent>(OnNestedCancel);
        SubscribeLocalEvent<XenoNestedComponent, ChangeDirectionAttemptEvent>(OnNestedCancel);
    }

    private void OnNestRemove(Entity<XenoNestComponent> ent, ref ComponentRemove args)
    {
        DetachNested(ent, ent.Comp.Nested);
    }

    private void OnNestTerminating(Entity<XenoNestComponent> ent, ref EntityTerminatingEvent args)
    {
        DetachNested(ent, ent.Comp.Nested);
    }

    private void OnNestedRemove(Entity<XenoNestedComponent> ent, ref ComponentRemove args)
    {
        DetachNested(null, ent);
    }

    private void OnNestCandidateInteractHand(Entity<XenoNestCandidateComponent> ent, ref InteractHandEvent args)
    {
        if (TryComp(args.User, out PullerComponent? puller) &&
            puller.Pulling is { } pulling)
        {
            var ev = new XenoNestDoAfterEvent();
            // TODO CM14 before merge delay
            var doAfter = new DoAfterArgs(EntityManager, args.User, TimeSpan.Zero, ev, ent, pulling);
            _doAfter.TryStartDoAfter(doAfter);
        }
    }

    private void OnNestCandidateDoAfter(Entity<XenoNestCandidateComponent> ent, ref XenoNestDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled)
            return;

        if (args.Target is not { } target)
            return;

        var targetCoords = _transform.GetMoverCoordinates(target);
        var nestCoords = _transform.GetMoverCoordinates(ent);
        if (!nestCoords.TryDelta(EntityManager, _transform, targetCoords, out var delta))
            return;

        var direction = (new Angle(delta) + - MathHelper.PiOver2).GetCardinalDir();

        if (ent.Comp.Nests.ContainsKey(direction))
            return;

        args.Handled = true;

        if (TryComp(target, out PullableComponent? pullable))
            _pulling.TryStopPull(target, pullable);

        if (_net.IsClient)
            return;

        var nestCoordinates = ent.Owner.ToCoordinates();
        var offset = direction switch
        {
            Direction.South => new Vector2(0, -0.25f),
            Direction.East => new Vector2(0.5f, 0),
            Direction.North => new Vector2(0, 0.5f),
            Direction.West => new Vector2(-0.5f, 0),
            _ => Vector2.Zero
        };

        var nest = SpawnAttachedTo(ent.Comp.Nest, nestCoordinates);
        _transform.SetCoordinates(nest, nestCoordinates.Offset(offset));

        ent.Comp.Nests[direction] = nest;
        Dirty(ent);

        var nestComp = EnsureComp<XenoNestComponent>(nest);
        nestComp.Nested = target;
        Dirty(nest, nestComp);

        var nestedComp = EnsureComp<XenoNestedComponent>(target);
        nestedComp.Nest = nest;
        Dirty(target, nestedComp);

        _transform.SetCoordinates(target, nest.ToCoordinates());
        _transform.SetLocalRotation(target, direction.ToAngle());
    }

    private void OnNestedPreventCollide(Entity<XenoNestedComponent> ent, ref PreventCollideEvent args)
    {
        args.Cancelled = true;
    }

    private void OnNestedPullAttempt(Entity<XenoNestedComponent> ent, ref PullAttemptEvent args)
    {
        args.Cancelled = true;
    }

    private void OnNestedCancel<T>(Entity<XenoNestedComponent> ent, ref T args) where T : CancellableEntityEventArgs
    {
        args.Cancel();
    }

    private void DetachNested(EntityUid? nest, EntityUid nested)
    {
        if (_timing.ApplyingState)
            return;

        if (TerminatingOrDeleted(nested) ||
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
        }

        if (TryComp(nest, out XenoNestCandidateComponent? candidate))
        {
            _candidateNests.Clear();
            foreach (var (dir, _) in candidate.Nests)
            {
                _candidateNests.Add(dir);
            }

            foreach (var dir in _candidateNests)
            {
                candidate.Nests.Remove(dir);
            }

            Dirty(nest.Value, candidate);
        }

        var position = xform.LocalPosition;
        _transform.SetLocalPosition(nested, position + xform.LocalRotation.ToWorldVec() / 2);
        _transform.AttachToGridOrMap(nested, xform);

        RemCompDeferred<XenoNestedComponent>(nested);
        QueueDel(nest);
    }
}
