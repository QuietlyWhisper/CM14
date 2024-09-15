using Content.Shared._RMC14.Xenonids.Egg;
using Content.Shared._RMC14.Xenonids.Evolution;
using Content.Shared._RMC14.Xenonids.Parasite;
using Content.Shared.Actions;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction;
using Content.Shared.Interaction.Events;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Systems;
using Content.Shared.Popups;
using Content.Shared.Stunnable;
using Content.Shared.Throwing;
using Robust.Shared.Containers;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Content.Shared._RMC14.Xenonids.Projectile.Parasite;

public sealed partial class XenoParasiteThrowerSystem : EntitySystem
{
    [Dependency] private readonly SharedActionsSystem _action = default!;
    [Dependency] private readonly SharedContainerSystem _container = default!;
    [Dependency] private readonly SharedHandsSystem _hands = default!;
    [Dependency] private readonly SharedInteractionSystem _interact = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly ThrowingSystem _throw = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly EntityManager _entities = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly SharedStunSystem _stun = default!;


    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<XenoParasiteThrowerComponent, XenoThrowParasiteActionEvent>(OnToggleParasiteThrow);
        SubscribeLocalEvent<XenoParasiteThrowerComponent, UserActivateInWorldEvent>(OnXenoParasiteThrowerUseInHand);
        SubscribeLocalEvent<XenoParasiteThrowerComponent, XenoEvolutionDoAfterEvent>(OnXenoEvolveDoAfter);
        SubscribeLocalEvent<XenoParasiteThrowerComponent, XenoDevolveBuiMsg>(OnXenoDevolveDoAfter);
        SubscribeLocalEvent<XenoParasiteThrowerComponent, MobStateChangedEvent>(OnDeathMobStateChanged);
    }

    private void OnToggleParasiteThrow(Entity<XenoParasiteThrowerComponent> xeno, ref XenoThrowParasiteActionEvent args)
    {
        var (ent, comp) = xeno;

        var target = args.Target;
        if (!_container.TryGetContainer(ent, XenoParasiteThrowerComponent.ParasiteContainerId, out var parasiteContainer))
        {
            return;
        }

        // If none of the entities on the selected, in-range tile are parasites, try to pull out a
        // parasite OR try to throw a held parasite
        if (_interact.InRangeUnobstructed(ent, target))
        {
            var clickedEntities = _lookup.GetEntitiesIntersecting(target);
            var tileHasParasites = false;
            foreach (var possibleParasite in clickedEntities)
            {
                if (_mobState.IsDead(possibleParasite))
                {
                    continue;
                }

                if (!HasComp<XenoParasiteComponent>(possibleParasite))
                {
                    continue;
                }

                tileHasParasites = true;

                if (parasiteContainer.Count >= comp.MaxParasites)
                {
                    _popup.PopupClient(Loc.GetString("cm-xeno-throw-parasite-too-many-parasites"), ent, ent);
                    return;
                }

                _container.Insert(possibleParasite, parasiteContainer);
            }

            if (tileHasParasites)
            {
                return;
            }
        }

        if (_hands.GetActiveItem(ent) is EntityUid heldEntity &&
            HasComp<XenoParasiteComponent>(heldEntity) &&
            !_mobState.IsDead(heldEntity))
        {
            _hands.TryDrop(ent);
            _throw.TryThrow(heldEntity, target);
            _stun.TryParalyze(heldEntity, comp.ThrownParasiteStunDuration, true);
            return;
        }

        if (parasiteContainer.Count == 0)
        {
            _popup.PopupClient(Loc.GetString("cm-xeno-throw-parasite-no-parasites"), ent, ent);
        }

        if (!parasiteContainer.ContainedEntities.TryFirstOrNull(out var parasite))
        {
            return;
        }

        _hands.TryPickupAnyHand(ent, parasite.Value);
    }

    private void OnXenoParasiteThrowerUseInHand(Entity<XenoParasiteThrowerComponent> xeno, ref UserActivateInWorldEvent args)
    {
        var (ent, comp) = xeno;
        var target = args.Target;

        if (!HasComp<XenoParasiteComponent>(target))
        {
            return;
        }

        if (_mobState.IsDead(target))
        {
            return;
        }

        if (args.Handled)
        {
            return;
        }

        if (!_container.TryGetContainer(ent, XenoParasiteThrowerComponent.ParasiteContainerId, out var parasiteContainer))
        {
            return;
        }

        if (parasiteContainer.Count >= comp.MaxParasites)
        {
            _popup.PopupClient(Loc.GetString("cm-xeno-throw-parasite-too-many-parasites"), ent, ent);
            return;
        }

        _container.Insert(target, parasiteContainer);
        _popup.PopupClient(Loc.GetString("cm-xeno-throw-parasite-stash-parasite"), ent, ent);
        args.Handled = true;
    }

    private void OnXenoEvolveDoAfter(Entity<XenoParasiteThrowerComponent> xeno, ref XenoEvolutionDoAfterEvent args)
    {
        DropAllStoredParasites(xeno);
    }

    private void OnXenoDevolveDoAfter(Entity<XenoParasiteThrowerComponent> xeno, ref XenoDevolveBuiMsg args)
    {
        DropAllStoredParasites(xeno);
    }

    private void OnDeathMobStateChanged(Entity<XenoParasiteThrowerComponent> xeno, ref MobStateChangedEvent args)
    {
        if (args.NewMobState != MobState.Dead)
            return;
        DropAllStoredParasites(xeno);
    }

    private bool DropAllStoredParasites(Entity<XenoParasiteThrowerComponent> xeno)
    {
        if (!_container.TryGetContainer(xeno.Owner, XenoParasiteThrowerComponent.ParasiteContainerId, out var parasiteContainer))
        {
            return false;
        }
        foreach (var parasite in parasiteContainer.ContainedEntities)
        {
            _transform.PlaceNextTo(xeno.Owner, parasite);
        }
        return true;
    }
}
