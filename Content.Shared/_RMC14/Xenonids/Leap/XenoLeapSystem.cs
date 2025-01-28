using System.Numerics;
using Content.Shared._RMC14.Pulling;
using Content.Shared._RMC14.Xenonids.Hive;
using Content.Shared._RMC14.Xenonids.Invisibility;
using Content.Shared._RMC14.Xenonids.Plasma;
using Content.Shared.ActionBlocker;
using Content.Shared.DoAfter;
using Content.Shared.Eye.Blinding.Systems;
using Content.Shared.FixedPoint;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Movement.Pulling.Components;
using Content.Shared.Movement.Pulling.Events;
using Content.Shared.Movement.Pulling.Systems;
using Content.Shared.Movement.Systems;
using Content.Shared.Popups;
using Content.Shared.Pulling.Events;
using Content.Shared.Standing;
using Content.Shared.Stunnable;
using Content.Shared.Weapons.Melee.Events;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Network;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Events;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Timing;

namespace Content.Shared._RMC14.Xenonids.Leap;

public sealed class XenoLeapSystem : EntitySystem
{
    [Dependency] private readonly ActionBlockerSystem _actionBlocker = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly BlindableSystem _blindable = default!;
    [Dependency] private readonly SharedBroadphaseSystem _broadphase = default!;
    [Dependency] private readonly SharedDoAfterSystem _doAfter = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly SharedXenoHiveSystem _hive = default!;
    [Dependency] private readonly MovementSpeedModifierSystem _movementSpeed = default!;
    [Dependency] private readonly INetManager _net = default!;
    [Dependency] private readonly SharedPhysicsSystem _physics = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly RMCPullingSystem _rmcPulling = default!;
    [Dependency] private readonly StandingStateSystem _standing = default!;
    [Dependency] private readonly SharedStunSystem _stun = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly XenoSystem _xeno = default!;
    [Dependency] private readonly XenoPlasmaSystem _xenoPlasma = default!;

    private EntityQuery<PhysicsComponent> _physicsQuery;

    public override void Initialize()
    {
        _physicsQuery = GetEntityQuery<PhysicsComponent>();

        SubscribeLocalEvent<XenoLeapComponent, XenoLeapActionEvent>(OnXenoLeapAction);
        SubscribeLocalEvent<XenoLeapComponent, XenoLeapDoAfterEvent>(OnXenoLeapDoAfter);
        SubscribeLocalEvent<XenoLeapComponent, MeleeHitEvent>(OnXenoLeapMelee);

        SubscribeLocalEvent<XenoLeapingComponent, StartCollideEvent>(OnXenoLeapingDoHit);
        SubscribeLocalEvent<XenoLeapingComponent, ComponentRemove>(OnXenoLeapingRemove);
        SubscribeLocalEvent<XenoLeapingComponent, PhysicsSleepEvent>(OnXenoLeapingPhysicsSleep);
        SubscribeLocalEvent<XenoLeapingComponent, StartPullAttemptEvent>(OnXenoLeapingStartPullAttempt);
        SubscribeLocalEvent<XenoLeapingComponent, PullAttemptEvent>(OnXenoLeapingPullAttempt);
    }

    private void OnXenoLeapAction(Entity<XenoLeapComponent> xeno, ref XenoLeapActionEvent args)
    {
        if (args.Handled)
            return;

        var attempt = new XenoLeapAttemptEvent();
        RaiseLocalEvent(xeno, ref attempt);

        if (attempt.Cancelled)
            return;

        if (xeno.Comp.PlasmaCost > FixedPoint2.Zero &&
            !_xenoPlasma.HasPlasmaPopup(xeno.Owner, xeno.Comp.PlasmaCost))
        {
            return;
        }

        args.Handled = true;

        var ev = new XenoLeapDoAfterEvent(GetNetCoordinates(args.Target));
        var doAfter = new DoAfterArgs(EntityManager, xeno, xeno.Comp.Delay, ev, xeno)
        {
            BreakOnMove = true,
            BreakOnDamage = true,
            DamageThreshold = FixedPoint2.New(10)
        };

        _doAfter.TryStartDoAfter(doAfter);
    }

    private void OnXenoLeapDoAfter(Entity<XenoLeapComponent> xeno, ref XenoLeapDoAfterEvent args)
    {
        if (args.Handled)
            return;

        if (args.Cancelled)
        {
            _popup.PopupClient(Loc.GetString("cm-xeno-leap-cancelled"), xeno, xeno);
            return;
        }

        if (!_physicsQuery.TryGetComponent(xeno, out var physics))
            return;

        if (EnsureComp<XenoLeapingComponent>(xeno, out var leaping))
            return;

        args.Handled = true;

        leaping.KnockdownRequiresInvisibility = xeno.Comp.KnockdownRequiresInvisibility;
        leaping.MoveDelayTime = xeno.Comp.MoveDelayTime;

        if (xeno.Comp.PlasmaCost > FixedPoint2.Zero &&
            !_xenoPlasma.TryRemovePlasmaPopup(xeno.Owner, xeno.Comp.PlasmaCost))
        {
            return;
        }

        _rmcPulling.TryStopAllPullsFromAndOn(xeno);

        var origin = _transform.GetMapCoordinates(xeno);
        var target = _transform.ToMapCoordinates(args.Coordinates);
        var direction = target.Position - origin.Position;

        if (direction == Vector2.Zero)
            return;

        var length = direction.Length();
        var distance = Math.Clamp(length, 0.1f, xeno.Comp.Range.Float());
        direction *= distance / length;
        var impulse = direction.Normalized() * xeno.Comp.Strength * physics.Mass;

        leaping.Origin = _transform.GetMoverCoordinates(xeno);
        leaping.ParalyzeTime = xeno.Comp.KnockdownTime;
        leaping.LeapSound = xeno.Comp.LeapSound;
        leaping.LeapEndTime = _timing.CurTime + TimeSpan.FromSeconds(direction.Length() / xeno.Comp.Strength);

        _physics.ApplyLinearImpulse(xeno, impulse, body: physics);
        _physics.SetBodyStatus(xeno, physics, BodyStatus.InAir);

        //Handle close-range or same-tile leaps
        foreach (var ent in _physics.GetContactingEntities(xeno.Owner, physics))
        {
            if (_hive.FromSameHive(xeno.Owner, ent))
                continue;

            if (ApplyLeapingHitEffects((xeno, leaping), ent))
                return;
        }
    }

    private void OnXenoLeapMelee(Entity<XenoLeapComponent> xeno, ref MeleeHitEvent args)
    {
        if (!xeno.Comp.UnrootOnMelee)
            return;

        if (!args.IsHit || args.HitEntities.Count == 0)
            return;

        foreach (var entity in args.HitEntities)
        {
            if (!_xeno.CanAbilityAttackTarget(xeno, entity))
                return;
        
            if (TryComp<SlowedDownComponent>(xeno, out var root) && root.SprintSpeedModifier == 0f)
            {
                RemComp<SlowedDownComponent>(xeno);
                _movementSpeed.RefreshMovementSpeedModifiers(xeno);
            }
            break;
        }
    }

    private void OnXenoLeapingDoHit(Entity<XenoLeapingComponent> xeno, ref StartCollideEvent args)
    {
        ApplyLeapingHitEffects(xeno, args.OtherEntity);
    }

    private void OnXenoLeapingRemove(Entity<XenoLeapingComponent> ent, ref ComponentRemove args)
    {
        StopLeap(ent);
    }

    private void OnXenoLeapingPhysicsSleep(Entity<XenoLeapingComponent> ent, ref PhysicsSleepEvent args)
    {
        StopLeap(ent);
    }

    private void OnXenoLeapingStartPullAttempt(Entity<XenoLeapingComponent> ent, ref StartPullAttemptEvent args)
    {
        args.Cancel();
    }

    private void OnXenoLeapingPullAttempt(Entity<XenoLeapingComponent> ent, ref PullAttemptEvent args)
    {
        args.Cancelled = true;
    }

    private bool IsValidLeapHit(Entity<XenoLeapingComponent> xeno, EntityUid target)
    {
        if (xeno.Comp.KnockedDown)
            return false;

        if (!HasComp<MobStateComponent>(target) || _mobState.IsIncapacitated(target))
            return false;

        if (_standing.IsDown(target))
            return false;

        if (HasComp<LeapIncapacitatedComponent>(target))
            return false;

        return true;
    }

    private bool ApplyLeapingHitEffects(Entity<XenoLeapingComponent> xeno, EntityUid target)
    {
        if(!IsValidLeapHit(xeno, target))
            return false;

        if (_hive.FromSameHive(xeno.Owner, target))
        {
            StopLeap(xeno);
            return true;
        }

        xeno.Comp.KnockedDown = true;
        Dirty(xeno);

        if (_physicsQuery.TryGetComponent(xeno, out var physics))
        {
            _physics.SetBodyStatus(xeno, physics, BodyStatus.OnGround);

            if (physics.Awake)
                _broadphase.RegenerateContacts(xeno, physics);
        }

        if (!xeno.Comp.KnockdownRequiresInvisibility || HasComp<XenoActiveInvisibleComponent>(xeno))
        {
            var victim = EnsureComp<LeapIncapacitatedComponent>(target);
            victim.RecoverAt = _timing.CurTime + xeno.Comp.ParalyzeTime;
            Dirty(target, victim);

            _stun.TrySlowdown(xeno, xeno.Comp.MoveDelayTime, true, 0f, 0f);

            if (_net.IsServer)
                _stun.TryParalyze(target, xeno.Comp.ParalyzeTime, true);
        }

        var ev = new XenoLeapHitEvent(xeno, target);
        RaiseLocalEvent(xeno, ref ev);

        if (!xeno.Comp.PlayedSound && _net.IsServer)
        {
            xeno.Comp.PlayedSound = true;
            _audio.PlayPvs(xeno.Comp.LeapSound, xeno);
        }

        StopLeap(xeno);
        return true;
    }

    private void StopLeap(Entity<XenoLeapingComponent> leaping)
    {
        if (_physicsQuery.TryGetComponent(leaping, out var physics))
        {
            _physics.SetLinearVelocity(leaping, Vector2.Zero, body: physics);
            _physics.SetBodyStatus(leaping, physics, BodyStatus.OnGround);
        }

        RemCompDeferred<XenoLeapingComponent>(leaping);
    }

    public override void Update(float frameTime)
    {
        var time = _timing.CurTime;
        var leaping = EntityQueryEnumerator<XenoLeapingComponent>();
        while (leaping.MoveNext(out var uid, out var comp))
        {
            if (time < comp.LeapEndTime)
                continue;

            StopLeap((uid, comp));
        }

        if (_net.IsClient)
            return;

        var incapacitated = EntityQueryEnumerator<LeapIncapacitatedComponent>();
        while (incapacitated.MoveNext(out var uid, out var victim))
        {
            if (victim.RecoverAt > time)
                continue;

            RemCompDeferred<LeapIncapacitatedComponent>(uid);
            _blindable.UpdateIsBlind(uid);
            _actionBlocker.UpdateCanMove(uid);
        }
    }
}
