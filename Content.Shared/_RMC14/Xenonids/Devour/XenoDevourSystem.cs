using System.Diagnostics.CodeAnalysis;
using Content.Shared._RMC14.Armor;
using Content.Shared._RMC14.Xenonids.Construction.Nest;
using Content.Shared.ActionBlocker;
using Content.Shared.Buckle.Components;
using Content.Shared.Damage;
using Content.Shared.DoAfter;
using Content.Shared.DragDrop;
using Content.Shared.Hands;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction;
using Content.Shared.Interaction.Events;
using Content.Shared.Inventory.Events;
using Content.Shared.Item;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Systems;
using Content.Shared.Movement.Events;
using Content.Shared.Movement.Pulling.Components;
using Content.Shared.Popups;
using Content.Shared.Stunnable;
using Content.Shared.Throwing;
using Content.Shared.Weapons.Melee.Events;
using Content.Shared.Weapons.Ranged.Events;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Containers;
using Robust.Shared.Network;
using Robust.Shared.Player;
using Robust.Shared.Timing;

namespace Content.Shared._RMC14.Xenonids.Devour;

public sealed class XenoDevourSystem : EntitySystem
{
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly ActionBlockerSystem _blocker = default!;
    [Dependency] private readonly SharedContainerSystem _container = default!;
    [Dependency] private readonly SharedDoAfterSystem _doAfter = default!;
    [Dependency] private readonly SharedHandsSystem _hands = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly INetManager _net = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly SharedStunSystem _stun = default!;
    [Dependency] private readonly EntityManager _entManager = default!;

    private EntityQuery<DevouredComponent> _devouredQuery;
    private EntityQuery<XenoDevourComponent> _xenoDevourQuery;

    public override void Initialize()
    {
        _devouredQuery = GetEntityQuery<DevouredComponent>();
        _xenoDevourQuery = GetEntityQuery<XenoDevourComponent>();

        SubscribeLocalEvent<DevourableComponent, CanDropDraggedEvent>(OnDevourableCanDropDragged);
        SubscribeLocalEvent<DevourableComponent, DragDropDraggedEvent>(OnDevourableDragDropDragged);
        SubscribeLocalEvent<DevourableComponent, BeforeRangedInteractEvent>(OnDevourableBeforeRangedInteract);

        SubscribeLocalEvent<DevouredComponent, ComponentStartup>(OnDevouredStartup);
        SubscribeLocalEvent<DevouredComponent, ComponentRemove>(OnDevouredRemove);
        SubscribeLocalEvent<DevouredComponent, EntGotRemovedFromContainerMessage>(OnDevouredRemovedFromContainer);
        SubscribeLocalEvent<DevouredComponent, InteractionAttemptEvent>(OnDevouredInteractionAttempt);
        SubscribeLocalEvent<DevouredComponent, UpdateCanMoveEvent>(OnDevouredAttempt);
        SubscribeLocalEvent<DevouredComponent, ThrowAttemptEvent>(OnDevouredAttempt);
        SubscribeLocalEvent<DevouredComponent, DropAttemptEvent>(OnDevouredAttempt);
        SubscribeLocalEvent<DevouredComponent, UseAttemptEvent>(OnUseAttempt);
        SubscribeLocalEvent<DevouredComponent, PickupAttemptEvent>(OnDevouredPickupAttempt);
        SubscribeLocalEvent<DevouredComponent, IsEquippingAttemptEvent>(OnDevouredIsEquippingAttempt);
        SubscribeLocalEvent<DevouredComponent, IsUnequippingAttemptEvent>(OnDevouredIsUnequippingAttempt);
        SubscribeLocalEvent<DevouredComponent, AttackAttemptEvent>(OnDevouredAttackAttempt);
        SubscribeLocalEvent<DevouredComponent, ShotAttemptedEvent>(OnDevouredShotAttempted);

        SubscribeLocalEvent<XenoDevourComponent, CanDropTargetEvent>(OnXenoCanDropTarget);
        SubscribeLocalEvent<XenoDevourComponent, ActivateInWorldEvent>(OnXenoActivate);
        SubscribeLocalEvent<XenoDevourComponent, DoAfterAttemptEvent<XenoDevourDoAfterEvent>>(OnXenoDevourDoAfterAttempt);
        SubscribeLocalEvent<XenoDevourComponent, XenoDevourDoAfterEvent>(OnXenoDevourDoAfter);
        SubscribeLocalEvent<XenoDevourComponent, XenoRegurgitateActionEvent>(OnXenoRegurgitateAction);
        SubscribeLocalEvent<XenoDevourComponent, EntityTerminatingEvent>(OnXenoTerminating);
        SubscribeLocalEvent<XenoDevourComponent, MobStateChangedEvent>(OnXenoMobStateChanged);

        SubscribeLocalEvent<UsableWhileDevouredComponent, GetMeleeDamageEvent>(OnUsableWhileDevouredGetMeleeDamage);
        SubscribeLocalEvent<UsableWhileDevouredComponent, GetMeleeAttackRateEvent>(OnUsableWhileDevouredGetMeleeAttackRate);
        SubscribeLocalEvent<UsableWhileDevouredComponent, CMGetArmorPiercingEvent>(OnUsableWhileDevouredGetArmorPiercing);
    }

    private void OnDevourableCanDropDragged(Entity<DevourableComponent> devourable, ref CanDropDraggedEvent args)
    {
        if (HasComp<XenoDevourComponent>(args.User))
        {
            args.CanDrop = true;
            args.Handled = true;
        }
    }

    private void OnDevourableDragDropDragged(Entity<DevourableComponent> devourable, ref DragDropDraggedEvent args)
    {
        if (args.User != args.Target)
            return;

        if (StartDevour(args.User, devourable))
            args.Handled = true;
    }

    private void OnDevourableBeforeRangedInteract(Entity<DevourableComponent> ent, ref BeforeRangedInteractEvent args)
    {
        if (args.User != args.Target)
            return;

        if (StartDevourPulled(args.User))
            args.Handled = true;
    }

    private void OnDevouredStartup(Entity<DevouredComponent> devoured, ref ComponentStartup args)
    {
        _blocker.UpdateCanMove(devoured);
    }

    private void OnDevouredRemove(Entity<DevouredComponent> devoured, ref ComponentRemove args)
    {
        _blocker.UpdateCanMove(devoured);

        if (_timing.ApplyingState)
            return;

        if (_container.TryGetContainingContainer((devoured, null), out var container) &&
            TryComp(container.Owner, out XenoDevourComponent? devour) &&
            container.ID != devour.DevourContainerId)
        {
            _container.Remove(devoured.Owner, container);
        }
    }

    private void OnDevouredRemovedFromContainer(Entity<DevouredComponent> devoured, ref EntGotRemovedFromContainerMessage args)
    {
        if (!_timing.ApplyingState)
            RemCompDeferred<DevouredComponent>(devoured);
    }
    private void OnDevouredInteractionAttempt(Entity<DevouredComponent> ent, ref InteractionAttemptEvent args)
    {
        if (args.Target == null)
            return;

        if (!HasComp<UsableWhileDevouredComponent>(args.Target))
            args.Cancelled = true;
    }

    private void OnDevouredAttempt<T>(Entity<DevouredComponent> devoured, ref T args) where T : CancellableEntityEventArgs
    {
        args.Cancel();
    }

    private void OnUseAttempt(Entity<DevouredComponent> ent, ref UseAttemptEvent args)
    {
        if (!HasComp<UsableWhileDevouredComponent>(args.Used))
            args.Cancel();
    }

    private void OnDevouredAttackAttempt(Entity<DevouredComponent> devoured, ref AttackAttemptEvent args)
    {
        if (!HasComp<UsableWhileDevouredComponent>(args.Weapon))
            args.Cancel();
    }

    private void OnDevouredPickupAttempt(Entity<DevouredComponent> ent, ref PickupAttemptEvent args)
    {
        if (!HasComp<UsableWhileDevouredComponent>(args.Item))
            args.Cancel();
    }

    private void OnDevouredIsEquippingAttempt(Entity<DevouredComponent> devoured, ref IsEquippingAttemptEvent args)
    {
        if (!HasComp<UsableWhileDevouredComponent>(args.Equipment))
            args.Cancel();
    }

    private void OnDevouredIsUnequippingAttempt(Entity<DevouredComponent> devoured, ref IsUnequippingAttemptEvent args)
    {
        if (TryComp<UsableWhileDevouredComponent>(args.Equipment, out var usableDevoured) && usableDevoured.CanUnequip)
            return;

        args.Cancel();
    }

    private void OnDevouredShotAttempted(Entity<DevouredComponent> devoured, ref ShotAttemptedEvent args)
    {
        if (!HasComp<GunUsableWhileDevouredComponent>(args.Used))
            args.Cancel();
    }

    private void OnXenoCanDropTarget(Entity<XenoDevourComponent> xeno, ref CanDropTargetEvent args)
    {
        if (args.Handled)
            return;

        if (HasComp<DevourableComponent>(args.Dragged) && xeno.Owner == args.User)
        {
            args.CanDrop = true;
            args.Handled = true;
        }
    }

    private void OnXenoActivate(Entity<XenoDevourComponent> xeno, ref ActivateInWorldEvent args)
    {
        if (args.User != args.Target)
            return;

        if (StartDevourPulled(args.User))
            args.Handled = true;
    }

    private void OnXenoDevourDoAfterAttempt(Entity<XenoDevourComponent> ent, ref DoAfterAttemptEvent<XenoDevourDoAfterEvent> args)
    {
        if (args.DoAfter.Args.Target is not { } target ||
            !CanDevour(ent, target, out _, true))
        {
            args.Cancel();
        }
    }

    private void OnXenoDevourDoAfter(Entity<XenoDevourComponent> xeno, ref XenoDevourDoAfterEvent args)
    {
        if (args.Handled || args.Cancelled || args.Target is not { } target)
            return;

        if (!CanDevour(xeno, target, out _, true))
            return;

        args.Handled = true;

        var container = _container.EnsureContainer<ContainerSlot>(xeno, xeno.Comp.DevourContainerId);
        if (!_container.Insert(target, container))
        {
            _popup.PopupClient(Loc.GetString("cm-xeno-devour-failed", ("target", target)), xeno, xeno, PopupType.SmallCaution);
            return;
        }

        var devoured = EnsureComp<DevouredComponent>(target);
        devoured.WarnAt = _timing.CurTime + xeno.Comp.WarnAfter;
        devoured.RegurgitateAt = _timing.CurTime + xeno.Comp.RegurgitateAfter;

        _popup.PopupClient(Loc.GetString("cm-xeno-devour-self", ("target", target)), xeno, xeno, PopupType.Medium);
        _popup.PopupEntity(Loc.GetString("cm-xeno-devour-target", ("user", xeno.Owner)), xeno, target, PopupType.MediumCaution);

        var others = Filter.PvsExcept(xeno).RemovePlayerByAttachedEntity(target);
        foreach (var session in others.Recipients)
        {
            if (session.AttachedEntity is not { } recipient)
                continue;

            _popup.PopupEntity(Loc.GetString("cm-xeno-devour-observer", ("user", xeno.Owner), ("target", target)), xeno, recipient, PopupType.MediumCaution);
        }

        var ev = new XenoDevouredEvent(target, xeno.Owner);
        RaiseLocalEvent(target, ref ev, true);
    }

    private void OnXenoRegurgitateAction(Entity<XenoDevourComponent> xeno, ref XenoRegurgitateActionEvent args)
    {
        if (!_container.TryGetContainer(xeno, xeno.Comp.DevourContainerId, out var container) ||
            container.ContainedEntities.Count == 0)
        {
            _popup.PopupClient(Loc.GetString("cm-xeno-none-devoured"), xeno, xeno);
            return;
        }

        args.Handled = true;
        var ents = _container.EmptyContainer(container);
        _popup.PopupClient(Loc.GetString("cm-xeno-devour-hurl-out"), xeno, xeno, PopupType.MediumCaution);
        _audio.PlayPredicted(xeno.Comp.RegurgitateSound, xeno, xeno);
        foreach (var ent in ents)
        {
            var ev = new RegurgitateEvent(_entManager.GetNetEntity(xeno.Owner), _entManager.GetNetEntity(ent));
            RaiseLocalEvent(xeno, ev);

            _stun.TryStun(ent, xeno.Comp.RegurgitationStun, true);
        }
    }

    private void OnXenoTerminating(Entity<XenoDevourComponent> xeno, ref EntityTerminatingEvent args)
    {
        if (_timing.ApplyingState)
            return;

        RegurgitateAll(xeno);
    }

    private void OnXenoMobStateChanged(Entity<XenoDevourComponent> xeno, ref MobStateChangedEvent args)
    {
        if (args.NewMobState != MobState.Dead)
            return;

        RegurgitateAll(xeno);
    }

    private void OnUsableWhileDevouredGetMeleeDamage(Entity<UsableWhileDevouredComponent> ent, ref GetMeleeDamageEvent args)
    {
        if (ent.Comp.Damage != null && IsHeldByDevoured(ent))
            args.Damage = new DamageSpecifier(ent.Comp.Damage);
    }

    private void OnUsableWhileDevouredGetMeleeAttackRate(Entity<UsableWhileDevouredComponent> ent, ref GetMeleeAttackRateEvent args)
    {
        if (IsHeldByDevoured(ent))
            args.Rate *= ent.Comp.AttackRateMultiplier;
    }

    private void OnUsableWhileDevouredGetArmorPiercing(Entity<UsableWhileDevouredComponent> ent, ref CMGetArmorPiercingEvent args)
    {
        if (IsHeldByDevoured(ent))
            args.Piercing += 100;
    }

    private bool IsHeldByDevoured(EntityUid item)
    {
        return _container.TryGetContainingContainer((item, null), out var marine) &&
               _devouredQuery.HasComp(marine.Owner) &&
               _hands.IsHolding(marine.Owner, item) &&
               _container.TryGetContainingContainer((marine.Owner, null), out var xeno) &&
               _xenoDevourQuery.TryComp(xeno.Owner, out var devour) &&
               xeno.ID == devour.DevourContainerId;
    }

    private bool CanDevour(EntityUid xeno, EntityUid victim, [NotNullWhen(true)] out XenoDevourComponent? devour, bool popup = false)
    {
        devour = default;
        if (xeno == victim ||
            !TryComp(xeno, out devour) ||
            HasComp<DevouredComponent>(victim) ||
            !HasComp<DevourableComponent>(victim))
        {
            return false;
        }

        if (_mobState.IsIncapacitated(xeno) ||
            HasComp<XenoNestedComponent>(victim))
        {
            if (popup)
                _popup.PopupClient(Loc.GetString("cm-xeno-devour-failed-cant-now"), victim, xeno);

            return false;
        }

        if (HasComp<XenoComponent>(victim))
        {
            if (popup)
                _popup.PopupClient(Loc.GetString("cm-xeno-devour-success"), victim, xeno);

            return false;
        }

        if (_mobState.IsDead(victim))
        {
            if (popup)
            {
                _popup.PopupClient(Loc.GetString("cm-xeno-devour-failed-target-roting", ("target", victim)), victim, xeno);
            }

            return false;
        }

        if (_container.TryGetContainer(xeno, devour.DevourContainerId, out var container) &&
            container.ContainedEntities.Count > 0)
        {
            devour = null;

            if (popup)
                _popup.PopupClient(Loc.GetString("cm-xeno-devour-failed-stomach-full"), victim, xeno, PopupType.SmallCaution);

            return false;
        }

        if (TryComp(victim, out BuckleComponent? buckle) && buckle.BuckledTo is { } strap)
        {
            if (popup)
            {
                _popup.PopupClient(Loc.GetString("cm-xeno-devour-failed-target-buckled", ("strap", strap), ("target", victim)), victim, xeno);
            }
        }

        return true;
    }

    private bool StartDevour(EntityUid xeno, EntityUid target)
    {
        if (!CanDevour(xeno, target, out var devour, true))
            return false;

        var doAfter = new DoAfterArgs(EntityManager, xeno, devour.DevourDelay, new XenoDevourDoAfterEvent(), xeno, target)
        {
            BreakOnMove = true,
            AttemptFrequency = AttemptFrequency.EveryTick
        };

        _popup.PopupClient(Loc.GetString("cm-xeno-devour-start-self", ("target", target)), target, xeno);

        _popup.PopupEntity(Loc.GetString("cm-xeno-devour-start-target", ("user", xeno)), xeno, target, PopupType.MediumCaution);

        var others = Filter.PvsExcept(xeno).RemovePlayerByAttachedEntity(target);
        foreach (var session in others.Recipients)
        {
            if (session.AttachedEntity is not { } recipient)
                continue;

            _popup.PopupEntity(Loc.GetString("cm-xeno-devour-start-observer", ("user", xeno), ("target", target)), target, recipient, PopupType.SmallCaution);
        }

        _doAfter.TryStartDoAfter(doAfter);
        return true;
    }

    private bool StartDevourPulled(EntityUid xeno)
    {
        if (CompOrNull<PullerComponent>(xeno)?.Pulling is not { } pulling)
            return false;

        return StartDevour(xeno, pulling);
    }

    private bool Regurgitate(Entity<DevouredComponent> devoured, Entity<XenoDevourComponent?> xeno, bool doFeedback = true)
    {
        if (!Resolve(xeno, ref xeno.Comp))
            return true;

        if (!_container.TryGetContainer(xeno, xeno.Comp.DevourContainerId, out var container) ||
            !_container.Remove(devoured.Owner, container))
        {
            return true;
        }

        var ev = new RegurgitateEvent(_entManager.GetNetEntity(xeno.Owner), _entManager.GetNetEntity(devoured.Owner));
        RaiseLocalEvent(xeno, ev);

        if (doFeedback)
            DoFeedback((xeno, xeno.Comp));

        return false;
    }

    private void RegurgitateAll(Entity<XenoDevourComponent> xeno)
    {
        if (!_container.TryGetContainer(xeno, xeno.Comp.DevourContainerId, out var container))
            return;

        var any = false;
        foreach (var contained in container.ContainedEntities)
        {
            if (TryComp(contained, out DevouredComponent? devoured) &&
                Regurgitate((contained, devoured), (xeno, xeno), false))
            {
                any = true;
            }
        }

        if (any)
            DoFeedback(xeno);
    }

    private void DoFeedback(Entity<XenoDevourComponent> xeno)
    {
        if (_net.IsServer)
        {
            _popup.PopupEntity(Loc.GetString("cm-xeno-devour-hurl-out"), xeno, xeno, PopupType.MediumCaution);
            _audio.PlayPvs(xeno.Comp.RegurgitateSound, xeno);
        }
    }

    public override void Update(float frameTime)
    {
        if (_net.IsClient)
            return;

        var time = _timing.CurTime;
        var devoured = EntityQueryEnumerator<DevouredComponent, TransformComponent>();
        while (devoured.MoveNext(out var uid, out var comp, out var xform))
        {
            if (!_container.TryGetContainingContainer((uid, xform), out var container) ||
                !TryComp(container.Owner, out XenoDevourComponent? devour) ||
                container.ID != devour.DevourContainerId)
            {
                RemCompDeferred<DevouredComponent>(uid);
                continue;
            }

            var xeno = container.Owner;
            if (_mobState.IsDead(uid))
            {
                Regurgitate((uid, comp), (xeno, devour));
                continue;
            }

            if (!comp.Warned && time >= comp.WarnAt)
            {
                comp.Warned = true;
                _popup.PopupEntity(Loc.GetString("cm-xeno-devour-regurgitate", ("target", uid)), xeno, xeno, PopupType.MediumCaution);
            }

            if (time >= comp.RegurgitateAt)
            {
                if (Regurgitate((uid, comp), (xeno, devour)))
                    _popup.PopupEntity(Loc.GetString("cm-xeno-devour-hurl-out"), xeno, xeno, PopupType.MediumCaution);
            }
        }
    }
}

/// <summary>
/// Event that is raised whenever a mob is devoured by another mob
/// </summary>
/// <param name="Target">The Entity who was devoured</param>
/// <param name="User">The Entity who caused the devouring</param>
[ByRefEvent]
public record struct XenoDevouredEvent(EntityUid Target, EntityUid User);
