﻿using Content.Shared._RMC14.Map;
using Content.Shared._RMC14.Marines.Skills;
using Content.Shared.Construction.Components;
using Content.Shared.Coordinates;
using Content.Shared.Damage;
using Content.Shared.Doors.Components;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction;
using Content.Shared.Maps;
using Content.Shared.Physics;
using Content.Shared.Popups;
using Content.Shared.Prototypes;
using Content.Shared.Stacks;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Network;
using Robust.Shared.Physics.Collision.Shapes;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Prototypes;

namespace Content.Shared._RMC14.Construction.Upgrades;

public sealed class RMCUpgradeSystem : EntitySystem
{
    [Dependency] private readonly IComponentFactory _compFactory = default!;
    [Dependency] private readonly FixtureSystem _fixture = default!;
    [Dependency] private readonly SharedMapSystem _map = default!;
    [Dependency] private readonly SkillsSystem _skills = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly SharedRMCMapSystem _rmcMap = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly SharedUserInterfaceSystem _ui = default!;
    [Dependency] private readonly SharedHandsSystem _hands = default!;
    [Dependency] private readonly SharedStackSystem _stack = default!;
    [Dependency] private readonly INetManager _net = default!;
    [Dependency] private readonly IPrototypeManager _prototypes = default!;
    [Dependency] private readonly DamageableSystem _damageable = default!;

    private readonly Dictionary<EntProtoId, RMCConstructionUpgradeComponent> _upgradePrototypes = new();
    private EntityQuery<RMCConstructionUpgradeItemComponent> _upgradeItemQuery;

    public override void Initialize()
    {
        _upgradeItemQuery = GetEntityQuery<RMCConstructionUpgradeItemComponent>();

        SubscribeLocalEvent<RMCConstructionUpgradeTargetComponent, InteractUsingEvent>(OnInteractUsing);

        Subs.BuiEvents<RMCConstructionUpgradeTargetComponent>(RMCConstructionUpgradeUiKey.Key,
            subs =>
            {
                subs.Event<RMCConstructionUpgradeBuiMsg>(OnUpgradeBuiMsg);
            });

        SubscribeLocalEvent<PrototypesReloadedEventArgs>(OnPrototypesReloaded);

        RefreshUpgradePrototypes();
    }

    private void OnInteractUsing(Entity<RMCConstructionUpgradeTargetComponent> ent, ref InteractUsingEvent args)
    {
        if (args.Handled)
            return;

        var user = args.User;
        var used = args.Used;

        if (!_skills.HasSkill(user, ent.Comp.Skill, ent.Comp.SkillAmountRequired))
        {
            var failPopup = Loc.GetString("rmc-construction-failure", ("ent", ent));
            _popup.PopupClient(failPopup, ent, user, PopupType.SmallCaution);
            args.Handled = true;
            return;
        }

        if (_upgradeItemQuery.HasComp(used) && ent.Comp.Upgrades != null)
        {
            _ui.OpenUi(ent.Owner, RMCConstructionUpgradeUiKey.Key, user);
            args.Handled = true;
        }
    }

    private void OnUpgradeBuiMsg(Entity<RMCConstructionUpgradeTargetComponent> ent, ref RMCConstructionUpgradeBuiMsg args)
    {
        _ui.CloseUi(ent.Owner, RMCConstructionUpgradeUiKey.Key);

        var user = args.Actor;

        if (!_upgradePrototypes.TryGetValue(args.Upgrade, out var upgradeComp))
            return;

        EntityUid? upgradeItem = null;

        foreach (var hand in _hands.EnumerateHands(user))
        {
            if (hand.HeldEntity == null)
                continue;

            if (_upgradeItemQuery.HasComp(hand.HeldEntity))
            {
                upgradeItem = hand.HeldEntity;
                break;
            }
        }

        if (upgradeItem == null)
            return;

        if (upgradeComp.Material != null)
        {
            if (TryComp<StackComponent>(upgradeItem, out var stack) && stack.StackTypeId == upgradeComp.Material)
            {
                if (!_stack.Use(upgradeItem.Value, upgradeComp.Amount, stack))
                {
                    var failPopup = Loc.GetString(upgradeComp.FailurePopup, ("ent", ent));
                    _popup.PopupClient(failPopup, ent, user, PopupType.SmallCaution);
                    return;
                }
            }
        }

        if (_net.IsClient)
            return;

        var coordinates = _transform.GetMapCoordinates(ent);
        var rotation = _transform.GetWorldRotation(ent);

        DamageSpecifier? transferredDamage = null;

        if (TryComp<DamageableComponent>(ent, out var damageComp))
            transferredDamage = damageComp.Damage;

        var spawn = Spawn(upgradeComp.UpgradedEntity, coordinates, rotation: rotation);
        _popup.PopupEntity(Loc.GetString(upgradeComp.UpgradedPopup), spawn, user);

        // transfer damage
        if (transferredDamage != null && TryComp<DamageableComponent>(spawn, out var newDamageComp))
            _damageable.SetDamage(spawn, newDamageComp, transferredDamage);

        var upgradeEv = new RMCConstructionUpgradedEvent(spawn, ent.Owner);
        RaiseLocalEvent(ent.Owner, upgradeEv);
        RaiseLocalEvent(spawn, upgradeEv, broadcast: true);

        QueueDel(ent);
    }

    private void OnPrototypesReloaded(PrototypesReloadedEventArgs ev)
    {
        if (ev.WasModified<EntityPrototype>())
            RefreshUpgradePrototypes();
    }

    private void RefreshUpgradePrototypes()
    {
        _upgradePrototypes.Clear();

        foreach (var prototype in _prototypes.EnumeratePrototypes<EntityPrototype>())
        {
            if (prototype.TryGetComponent(out RMCConstructionUpgradeComponent? upgrade, _compFactory))
                _upgradePrototypes[prototype.ID] = upgrade;
        }
    }
}

/// <summary>
///     This event that is raised when an entity is ugraded
/// </summary>
public sealed class RMCConstructionUpgradedEvent : EntityEventArgs
{
    public readonly EntityUid New;
    public readonly EntityUid Old;

    public RMCConstructionUpgradedEvent(EntityUid newUid, EntityUid oldUid)
    {
        New = newUid;
        Old = oldUid;
    }
}
