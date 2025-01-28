using Content.Shared._RMC14.Standing;
using Content.Shared._RMC14.Xenonids;
using Content.Shared.Movement.Systems;
using Content.Shared.Rejuvenate;
using Content.Shared.Standing;
using Content.Shared.StatusEffect;
using Content.Shared.Stunnable;
using Robust.Shared.Network;
using Robust.Shared.Timing;

namespace Content.Shared._RMC14.Slow;

public sealed class RMCSlowSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly INetManager _net = default!;
    [Dependency] private readonly MovementSpeedModifierSystem _speed = default!;
    [Dependency] private readonly StandingStateSystem _standing = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<RMCSlowdownComponent, ComponentStartup>(OnAdded);
        SubscribeLocalEvent<RMCSuperSlowdownComponent, ComponentStartup>(OnAdded);
        SubscribeLocalEvent<RMCRootedComponent, ComponentStartup>(OnAdded);

        SubscribeLocalEvent<RMCSlowdownComponent, ComponentShutdown>(OnExpire);
        SubscribeLocalEvent<RMCSuperSlowdownComponent, ComponentShutdown>(OnExpire);
        SubscribeLocalEvent<RMCRootedComponent, ComponentShutdown>(OnExpire);

        SubscribeLocalEvent<RMCSlowdownComponent, RejuvenateEvent>(OnRejuvenate);
        SubscribeLocalEvent<RMCSuperSlowdownComponent, RejuvenateEvent>(OnRejuvenate);
        SubscribeLocalEvent<RMCRootedComponent, RejuvenateEvent>(OnRejuvenate);

        SubscribeLocalEvent<RMCSlowdownComponent, RefreshMovementSpeedModifiersEvent>(OnSlowdownRefresh);
        SubscribeLocalEvent<RMCSuperSlowdownComponent, RefreshMovementSpeedModifiersEvent>(OnSuperSlowdownRefresh);
        SubscribeLocalEvent<RMCRootedComponent, RefreshMovementSpeedModifiersEvent>(OnRootRefresh);

        SubscribeLocalEvent<RMCSpeciesSlowdownModifierComponent, StunnedEvent>(OnModifierStun);
        SubscribeLocalEvent<RMCSpeciesSlowdownModifierComponent, KnockedDownEvent>(OnModifierKnockdown);
        SubscribeLocalEvent<RMCSpeciesSlowdownModifierComponent, StatusEffectEndedEvent>(OnModifierEffectEnd);
    }

    public bool TrySlowdown(EntityUid ent, TimeSpan duration, bool refresh = true, bool ignoreDurationModifier = false)
    {
        if (!TryComp<RMCSpeciesSlowdownModifierComponent>(ent, out var slow))
            return false;

        var expire = _timing.CurTime + duration * (ignoreDurationModifier ? 1 : slow.DurationMultiplier);

        var slowdown = EnsureComp<RMCSlowdownComponent>(ent);

        if (refresh && expire > slowdown.ExpiresAt)
            slowdown.ExpiresAt = expire;
        else if (!refresh) // Stacks
            slowdown.ExpiresAt += duration;

        return true;
    }

    public bool TrySuperSlowdown(EntityUid ent, TimeSpan duration, bool refresh = true, bool ignoreDurationModifier = false)
    {
        if (!TryComp<RMCSpeciesSlowdownModifierComponent>(ent, out var slow))
            return false;

        var expire = _timing.CurTime + duration * (ignoreDurationModifier ? 1 : slow.DurationMultiplier);

        var slowdown = EnsureComp<RMCSuperSlowdownComponent>(ent);

        if (refresh && expire > slowdown.ExpiresAt)
            slowdown.ExpiresAt = expire;
        else if (!refresh) // Stacks
            slowdown.ExpiresAt += duration;

        return true;
    }

    public bool TryRoot(EntityUid ent, TimeSpan duration, bool refresh = true)
    {
        var expire = _timing.CurTime + duration;

        var slowdown = EnsureComp<RMCRootedComponent>(ent);

        if (refresh && expire > slowdown.ExpiresAt)
            slowdown.ExpiresAt = expire;
        else if (!refresh) // Stacks
            slowdown.ExpiresAt += duration;

        return true;
    }

    private void OnAdded<T>(Entity<T> ent, ref ComponentStartup args) where T : IComponent
    {
        _speed.RefreshMovementSpeedModifiers(ent);

        if (HasComp<XenoComponent>(ent))
            return;

        if (typeof(T) != typeof(RMCRootedComponent))
            EnsureComp<XenoSlowVisualsComponent>(ent);
        else
            EnsureComp<XenoImmobileVisualsComponent>(ent);
    }

    private void OnExpire<T>(Entity<T> ent, ref ComponentShutdown args) where T : IComponent
    {
        if (!TerminatingOrDeleted(ent))
            _speed.RefreshMovementSpeedModifiers(ent);
        if (typeof(T) != typeof(RMCRootedComponent))
        {
            if (typeof(T) == typeof(RMCSlowdownComponent))
                MaybeRemoveSlowVisuals(ent);
            else
                MaybeRemoveSuperSlowVisuals(ent);
        }
        else
            MaybeRemoveStunVisuals(ent);
    }

    private void OnRejuvenate<T>(Entity<T> ent, ref RejuvenateEvent args) where T : IComponent
    {
        RemCompDeferred<T>(ent);
    }

    private void OnSlowdownRefresh(Entity<RMCSlowdownComponent> ent, ref RefreshMovementSpeedModifiersEvent args)
    {
        if (!TryComp<RMCSpeciesSlowdownModifierComponent>(ent, out var slow))
            return;

        //Don't apply slow when superslow is in effect
        if (!HasComp<RMCSuperSlowdownComponent>(ent))
            args.ModifySpeed(slow.SlowMultiplier, slow.SlowMultiplier);
    }

    private void OnSuperSlowdownRefresh(Entity<RMCSuperSlowdownComponent> ent, ref RefreshMovementSpeedModifiersEvent args)
    {
        if (!TryComp<RMCSpeciesSlowdownModifierComponent>(ent, out var slow))
            return;

        args.ModifySpeed(slow.SuperSlowMultiplier, slow.SuperSlowMultiplier);
    }

    private void OnRootRefresh(Entity<RMCRootedComponent> ent, ref RefreshMovementSpeedModifiersEvent args)
    {
        args.ModifySpeed(0, 0);
    }

    private void MaybeRemoveSlowVisuals(EntityUid ent)
    {
        if (!HasComp<XenoSlowVisualsComponent>(ent))
            return;

        if (HasComp<RMCSuperSlowdownComponent>(ent))
            return;

        RemCompDeferred<XenoSlowVisualsComponent>(ent);
    }

    private void MaybeRemoveSuperSlowVisuals(EntityUid ent)
    {
        if (!HasComp<XenoSlowVisualsComponent>(ent))
            return;

        if (HasComp<RMCSlowdownComponent>(ent))
            return;

        RemCompDeferred<XenoSlowVisualsComponent>(ent);
    }

    private void MaybeRemoveStunVisuals(EntityUid ent)
    {
        if (!HasComp<XenoImmobileVisualsComponent>(ent))
            return;

        if (HasComp<StunnedComponent>(ent) && !_standing.IsDown(ent))
            return;

        RemCompDeferred<XenoImmobileVisualsComponent>(ent);
    }

    private void OnModifierStun(Entity<RMCSpeciesSlowdownModifierComponent> ent, ref StunnedEvent args)
    {
        if (_standing.IsDown(ent))
            return;

        EnsureComp<XenoImmobileVisualsComponent>(ent);
    }

    private void OnModifierKnockdown(Entity<RMCSpeciesSlowdownModifierComponent> ent, ref KnockedDownEvent args)
    {
        if (!HasComp<XenoImmobileVisualsComponent>(ent))
            return;

        RemCompDeferred<XenoImmobileVisualsComponent>(ent);
    }

    private void OnModifierEffectEnd(Entity<RMCSpeciesSlowdownModifierComponent> ent, ref StatusEffectEndedEvent args)
    {
        if (args.Key != "Stun" && args.Key != "KnockedDown")
            return;

        if (args.Key == "Stun" && !HasComp<RMCRootedComponent>(ent))
            RemCompDeferred<XenoImmobileVisualsComponent>(ent);
        else if ((args.Key == "KnockedDown" || !_standing.IsDown(ent)) && HasComp<StunnedComponent>(ent))
            EnsureComp<XenoImmobileVisualsComponent>(ent);
    }

    public override void Update(float frameTime)
    {
        if (_net.IsClient)
            return;

        var time = _timing.CurTime;

        var slowQuery = EntityQueryEnumerator<RMCSlowdownComponent>();

        while (slowQuery.MoveNext(out var uid, out var slow))
        {
            if (time < slow.ExpiresAt)
                continue;

            RemCompDeferred<RMCSlowdownComponent>(uid);
        }

        var superSlowQuery = EntityQueryEnumerator<RMCSuperSlowdownComponent>();

        while (superSlowQuery.MoveNext(out var uid, out var slow))
        {
            if (time < slow.ExpiresAt)
                continue;

            RemCompDeferred<RMCSuperSlowdownComponent>(uid);
        }

        var rootQuery = EntityQueryEnumerator<RMCRootedComponent>();

        while (rootQuery.MoveNext(out var uid, out var root))
        {
            if (time < root.ExpiresAt)
                continue;

            RemCompDeferred<RMCRootedComponent>(uid);
        }
    }
}
