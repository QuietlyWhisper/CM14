using System.Numerics;
using Content.Shared._RMC14.Fireman;
using Content.Shared._RMC14.Xenonids;
using Content.Shared._RMC14.Xenonids.Parasite;
using Content.Shared.ActionBlocker;
using Content.Shared.Coordinates;
using Content.Shared.Interaction;
using Content.Shared.Interaction.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.MouseRotator;
using Content.Shared.Movement.Components;
using Content.Shared.Movement.Pulling.Components;
using Content.Shared.Movement.Pulling.Events;
using Content.Shared.Movement.Pulling.Systems;
using Content.Shared.Movement.Systems;
using Content.Shared.Popups;
using Content.Shared.StatusEffect;
using Content.Shared.Stunnable;
using Content.Shared.Weapons.Melee;
using Content.Shared.Whitelist;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Network;
using Robust.Shared.Random;
using Robust.Shared.Timing;


namespace Content.Shared._RMC14.Pulling;

public sealed class RMCPullingSystem : EntitySystem
{
    [Dependency] private readonly ActionBlockerSystem _actionBlocker = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly SharedMeleeWeaponSystem _melee = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly MovementSpeedModifierSystem _movementSpeed = default!;
    [Dependency] private readonly SharedXenoParasiteSystem _parasite = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly PullingSystem _pulling = default!;
    [Dependency] private readonly INetManager _net = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly StatusEffectsSystem _statusEffects = default!;
    [Dependency] private readonly SharedStunSystem _stun = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly EntityWhitelistSystem _whitelist = default!;
    [Dependency] private readonly RotateToFaceSystem _rotateTo = default!;

    private readonly SoundSpecifier _pullSound = new SoundPathSpecifier("/Audio/Effects/thudswoosh.ogg")
    {
        Params = AudioParams.Default.WithVariation(0.05f),
    };

    private const string PullEffect = "CMEffectGrab";

    private EntityQuery<FiremanCarriableComponent> _firemanQuery;

    public override void Initialize()
    {
        _firemanQuery = GetEntityQuery<FiremanCarriableComponent>();

        SubscribeLocalEvent<ParalyzeOnPullAttemptComponent, PullAttemptEvent>(OnParalyzeOnPullAttempt);
        SubscribeLocalEvent<InfectOnPullAttemptComponent, PullAttemptEvent>(OnInfectOnPullAttempt);

        SubscribeLocalEvent<SlowOnPullComponent, PullStartedMessage>(OnSlowPullStarted);
        SubscribeLocalEvent<SlowOnPullComponent, PullStoppedMessage>(OnSlowPullStopped);

        SubscribeLocalEvent<PullingSlowedComponent, RefreshMovementSpeedModifiersEvent>(OnPullingSlowedMovementSpeed);

        SubscribeLocalEvent<PullWhitelistComponent, PullAttemptEvent>(OnPullWhitelistPullAttempt);

        SubscribeLocalEvent<BlockPullingDeadComponent, PullAttemptEvent>(OnBlockDeadPullAttempt);
        SubscribeLocalEvent<BlockPullingDeadComponent, PullStartedMessage>(OnBlockDeadPullStarted);
        SubscribeLocalEvent<BlockPullingDeadComponent, PullStoppedMessage>(OnBlockDeadPullStopped);

        SubscribeLocalEvent<PreventPulledWhileAliveComponent, PullAttemptEvent>(OnPreventPulledWhileAliveAttempt);
        SubscribeLocalEvent<PreventPulledWhileAliveComponent, PullStartedMessage>(OnPreventPulledWhileAliveStart);
        SubscribeLocalEvent<PreventPulledWhileAliveComponent, PullStoppedMessage>(OnPreventPulledWhileAliveStop);

        SubscribeLocalEvent<PullableComponent, PullStartedMessage>(OnPullAnimation);

        SubscribeLocalEvent<PullerComponent, PullStoppedMessage>(OnPullerPullStopped);

        SubscribeLocalEvent<BeingPulledComponent, PullStoppedMessage>(OnBeingPulledPullStopped);
    }

    private void OnParalyzeOnPullAttempt(Entity<ParalyzeOnPullAttemptComponent> ent, ref PullAttemptEvent args)
    {
        var user = args.PullerUid;
        var target = args.PulledUid;
        if (target != ent.Owner ||
            HasComp<ParalyzeOnPullAttemptImmuneComponent>(user) ||
            _mobState.IsDead(ent))
        {
            return;
        }

        args.Cancelled = true;

        if (ent.Comp.Sound is { } sound)
        {
            var pitch = _random.NextFloat(ent.Comp.MinPitch, ent.Comp.MaxPitch);
            _audio.PlayPredicted(sound, ent, user, sound.Params.WithPitchScale(pitch));
        }

        _stun.TryParalyze(user, ent.Comp.Duration, true);

        var puller = user;
        var pulled = target;
        var othersMessage = Loc.GetString("rmc-pull-paralyze-others", ("puller", puller), ("pulled", pulled));
        var selfMessage = Loc.GetString("rmc-pull-paralyze-self", ("puller", puller), ("pulled", pulled));

        _popup.PopupPredicted(selfMessage, othersMessage, puller, puller, PopupType.MediumCaution);
    }

    private void OnInfectOnPullAttempt(Entity<InfectOnPullAttemptComponent> ent, ref PullAttemptEvent args)
    {
        var user = args.PullerUid;
        var target = args.PulledUid;
        if (target != ent.Owner ||
            HasComp<InfectOnPullAttemptImmuneComponent>(user) ||
            _mobState.IsDead(ent))
        {
            return;
        }

        if (!TryComp<XenoParasiteComponent>(target, out var paraComp))
            return;

        Entity<XenoParasiteComponent> comp = (target, paraComp);
        args.Cancelled = true;

        if (!_parasite.Infect(comp, user, false, true))
            return;

        var puller = user;
        var pulled = target;
        var othersMessage = Loc.GetString("rmc-pull-infect-others", ("puller", puller), ("pulled", pulled));
        var selfMessage = Loc.GetString("rmc-pull-infect-self", ("puller", puller), ("pulled", pulled));

        _popup.PopupPredicted(selfMessage, othersMessage, puller, puller, PopupType.MediumCaution);
    }

    private void OnSlowPullStarted(Entity<SlowOnPullComponent> ent, ref PullStartedMessage args)
    {
        if (ent.Owner == args.PullerUid)
        {
            EnsureComp<PullingSlowedComponent>(args.PullerUid);
            _movementSpeed.RefreshMovementSpeedModifiers(args.PullerUid);
        }
    }

    private void OnSlowPullStopped(Entity<SlowOnPullComponent> ent, ref PullStoppedMessage args)
    {
        if (ent.Owner == args.PullerUid)
        {
            RemComp<PullingSlowedComponent>(args.PullerUid);
            _movementSpeed.RefreshMovementSpeedModifiers(args.PullerUid);
        }
    }

    private void OnPullingSlowedMovementSpeed(Entity<PullingSlowedComponent> ent, ref RefreshMovementSpeedModifiersEvent args)
    {
        if (HasComp<BypassInteractionChecksComponent>(ent) ||
            !TryComp(ent, out PullerComponent? puller) ||
            !TryComp(ent, out SlowOnPullComponent? slow))
        {
            return;
        }

        if (puller.Pulling == null)
            return;

        var ev = new PullSlowdownAttemptEvent(puller.Pulling.Value);
        RaiseLocalEvent(ent, ref ev);
        if (ev.Cancelled)
            return;

        foreach (var slowdown in slow.Slowdowns)
        {
            if (_whitelist.IsWhitelistPass(slowdown.Whitelist, puller.Pulling.Value))
            {
                args.ModifySpeed(slowdown.Multiplier, slowdown.Multiplier);
                return;
            }
        }

        args.ModifySpeed(slow.Multiplier, slow.Multiplier);
    }

    private void OnPullWhitelistPullAttempt(Entity<PullWhitelistComponent> ent, ref PullAttemptEvent args)
    {
        if (args.Cancelled || ent.Owner == args.PulledUid)
            return;

        if (!_whitelist.IsValid(ent.Comp.Whitelist, args.PulledUid))
        {
            _popup.PopupClient(Loc.GetString("cm-pull-whitelist-denied", ("name", args.PulledUid)), args.PulledUid, args.PullerUid);
            args.Cancelled = true;
        }
    }

    private void OnBlockDeadPullAttempt(Entity<BlockPullingDeadComponent> ent, ref PullAttemptEvent args)
    {
        if (args.Cancelled || ent.Owner == args.PulledUid)
            return;

        if (_mobState.IsDead(args.PulledUid) && !HasComp<IgnoreBlockPullingDeadComponent>(args.PulledUid))
        {
            _popup.PopupClient(Loc.GetString("cm-pull-whitelist-denied-dead", ("name", args.PulledUid)), args.PulledUid, args.PullerUid);
            args.Cancelled = true;
        }
    }

    private void OnBlockDeadPullStarted(Entity<BlockPullingDeadComponent> ent, ref PullStartedMessage args)
    {
        if (ent.Owner == args.PullerUid)
            EnsureComp<BlockPullingDeadActiveComponent>(ent);
    }

    private void OnBlockDeadPullStopped(Entity<BlockPullingDeadComponent> ent, ref PullStoppedMessage args)
    {
        if (ent.Owner == args.PullerUid)
            RemCompDeferred<BlockPullingDeadActiveComponent>(ent);
    }

    private void OnPreventPulledWhileAliveAttempt(Entity<PreventPulledWhileAliveComponent> ent, ref PullAttemptEvent args)
    {
        if (args.PulledUid != ent.Owner)
            return;

        if (!CanPullPreventPulledWhileAlive((ent, ent), args.PullerUid))
        {
            var msg = Loc.GetString("rmc-prevent-pull-alive", ("target", ent));
            _popup.PopupClient(msg, ent, args.PullerUid, PopupType.SmallCaution);
            args.Cancelled = true;
        }
    }

    private void OnPreventPulledWhileAliveStart(Entity<PreventPulledWhileAliveComponent> ent, ref PullStartedMessage args)
    {
        if (args.PulledUid != ent.Owner)
            return;

        EnsureComp<ActivePreventPulledWhileAliveComponent>(ent);
    }

    private void OnPreventPulledWhileAliveStop(Entity<PreventPulledWhileAliveComponent> ent, ref PullStoppedMessage args)
    {
        if (args.PulledUid != ent.Owner)
            return;

        RemCompDeferred<ActivePreventPulledWhileAliveComponent>(ent);
    }

    private bool CanPullPreventPulledWhileAlive(Entity<PreventPulledWhileAliveComponent?> pulled, EntityUid user)
    {
        if (!Resolve(pulled, ref pulled.Comp, false))
            return true;

        if (!_mobState.IsAlive(pulled))
            return true;

        if (!_whitelist.IsWhitelistPassOrNull(pulled.Comp.Whitelist, user))
            return true;

        foreach (var effect in pulled.Comp.ExceptEffects)
        {
            if (_statusEffects.HasStatusEffect(pulled, effect))
                return true;
        }

        return false;
    }

    public void TryStopUserPullIfPulling(EntityUid user, EntityUid target)
    {
        if (!TryComp(user, out PullerComponent? puller) ||
            puller.Pulling != target ||
            !TryComp(puller.Pulling, out PullableComponent? pullable))
        {
            return;
        }

        _pulling.TryStopPull(puller.Pulling.Value, pullable, user);
    }

    public void TryStopPullsOn(EntityUid puller)
    {
        if (!TryComp<PullableComponent>(puller, out var pullable) ||
             pullable.Puller == null)
        {
            return;
        }

        _pulling.TryStopPull(puller, pullable);
    }

    public void TryStopAllPullsFromAndOn(EntityUid pullie)
    {
        TryStopPullsOn(pullie);

       if (TryComp(pullie, out PullerComponent? puller) &&
            puller.Pulling != null &&
            TryComp(puller.Pulling, out PullableComponent? pullable2))
        {
            _pulling.TryStopPull(puller.Pulling.Value, pullable2, pullie);
            return;
        }
    }

    private void OnPullAnimation(Entity<PullableComponent> ent, ref PullStartedMessage args)
    {
        if (args.PulledUid != ent.Owner)
            return;

        if (!_timing.ApplyingState)
            EnsureComp<BeingPulledComponent>(ent);

        PlayPullEffect(args.PullerUid, args.PulledUid);
    }

    private void OnBeingPulledPullStopped(Entity<BeingPulledComponent> ent, ref PullStoppedMessage args)
    {
        if (args.PulledUid != ent.Owner)
            return;

        if (_timing.ApplyingState)
            return;

        RemCompDeferred<BeingPulledComponent>(ent);
    }

    private void OnPullerPullStopped(Entity<PullerComponent> ent, ref PullStoppedMessage args)
    {
        if (args.PulledUid == ent.Owner)
            return;

        if (!_timing.ApplyingState && !HasComp<MouseRotatorComponent>(ent))
            RemCompDeferred<NoRotateOnMoveComponent>(ent);
    }

    public bool IsPulling(Entity<PullerComponent?> user, Entity<PullableComponent?> target)
    {
        if (!Resolve(user, ref user.Comp, false) ||
            !Resolve(target, ref target.Comp, false))
        {
            return false;
        }

        return user.Comp.Pulling == target;
    }

    public bool IsBeingPulled(Entity<PullableComponent?> target, out EntityUid user)
    {
        user = default;
        if (!Resolve(target, ref target.Comp, false))
            return false;

        if (target.Comp.Puller is { } puller)
            user = puller;

        return target.Comp.BeingPulled;
    }

    public void PlayPullEffect(EntityUid puller, EntityUid pulled)
    {
        var userXform = Transform(puller);
        var targetPos = _transform.GetWorldPosition(pulled);
        var localPos = Vector2.Transform(targetPos, _transform.GetInvWorldMatrix(userXform));
        localPos = userXform.LocalRotation.RotateVec(localPos);

        _melee.DoLunge(puller, puller, Angle.Zero, localPos, null);
        _audio.PlayPredicted(_pullSound, pulled, puller);

        if (_net.IsServer)
            SpawnAttachedTo(PullEffect, pulled.ToCoordinates());
    }

    public override void Update(float frameTime)
    {
        var blockDeadActive = EntityQueryEnumerator<BlockPullingDeadActiveComponent, PullerComponent>();
        while (blockDeadActive.MoveNext(out var uid, out _, out var puller))
        {
            if (puller.Pulling is not { } pulling ||
                !TryComp(pulling, out PullableComponent? pullable))
            {
                continue;
            }

            if (_mobState.IsDead(pulling) && !HasComp<IgnoreBlockPullingDeadComponent>(pulling))
                _pulling.TryStopPull(pulling, pullable, uid);
        }

        var preventPulledWhileAlive = EntityQueryEnumerator<ActivePreventPulledWhileAliveComponent, PreventPulledWhileAliveComponent, PullableComponent>();
        while (preventPulledWhileAlive.MoveNext(out var uid, out _, out var prevent, out var pullable))
        {
            if (pullable.Puller is not { } puller ||
                CanPullPreventPulledWhileAlive((uid, prevent), puller))
            {
                continue;
            }

            _pulling.TryStopPull(uid, pullable);
        }

        var pulledQuery = EntityQueryEnumerator<BeingPulledComponent, InputMoverComponent, PullableComponent>();
        while (pulledQuery.MoveNext(out var uid, out _, out var input, out var pullable))
        {
            if ((input.HeldMoveButtons & MoveButtons.AnyDirection) == 0)
                continue;

            if (!_actionBlocker.CanMove(uid))
                continue;

            _pulling.TryStopPull(uid, pullable);
        }

        var pullableQuery = EntityQueryEnumerator<BeingPulledComponent, PullableComponent>();
        while (pullableQuery.MoveNext(out var uid, out _, out var pullable))
        {
            if (pullable.Puller == null)
                continue;

            var puller = pullable.Puller.Value;
            if (!Exists(puller))
                continue;

            if (_firemanQuery.TryComp(uid, out var fireman) && fireman.BeingCarried)
                continue;

            if (HasComp<MouseRotatorComponent>(puller))
                continue;

            if (!_timing.ApplyingState)
                EnsureComp<NoRotateOnMoveComponent>(puller);

            var pulledCoords = _transform.GetMapCoordinates(uid).Position;
            var pullerCoords = _transform.GetMapCoordinates(puller).Position;

            var angle = (pulledCoords - pullerCoords).ToWorldAngle().GetCardinalDir().ToAngle();
            _rotateTo.TryFaceAngle(puller, angle);
        }
    }
}
