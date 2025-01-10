﻿using System.Linq;
using Content.Shared.Actions;
using Content.Shared.Interaction;
using Content.Shared.Item;
using Content.Shared.Popups;
using Content.Shared.PowerCell;
using Content.Shared.PowerCell.Components;
using Content.Shared.Storage.EntitySystems;
using Content.Shared.Tools.Systems;
using Robust.Shared.Containers;
using Robust.Shared.Utility;

namespace Content.Shared._RMC14.Visor;

public sealed class VisorSystem : EntitySystem
{
    [Dependency] private readonly SharedContainerSystem _container = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly SharedPowerCellSystem _powerCell = default!;
    [Dependency] private readonly SharedToolSystem _tool = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<CycleableVisorComponent, GetItemActionsEvent>(OnCycleableVisorGetItemActions);
        SubscribeLocalEvent<CycleableVisorComponent, CycleVisorActionEvent>(OnCycleableVisorAction);
        SubscribeLocalEvent<CycleableVisorComponent, InteractUsingEvent>(OnCycleableVisorInteractUsing, before: [typeof(SharedStorageSystem)]);

        SubscribeLocalEvent<VisorComponent, ActivateVisorEvent>(OnVisorActivate);
        SubscribeLocalEvent<VisorComponent, DeactivateVisorEvent>(OnVisorDeactivate);
        SubscribeLocalEvent<VisorComponent, PowerCellChangedEvent>(OnCycleableVisorPowerCellChanged);
    }

    private void OnCycleableVisorGetItemActions(Entity<CycleableVisorComponent> ent, ref GetItemActionsEvent args)
    {
        args.AddAction(ref ent.Comp.Action, ent.Comp.ActionId);
    }

    private void OnCycleableVisorAction(Entity<CycleableVisorComponent> ent, ref CycleVisorActionEvent args)
    {
        var containers = new List<ContainerSlot>();
        foreach (var id in ent.Comp.Containers)
        {
            containers.Add(_container.EnsureContainer<ContainerSlot>(ent, id));
        }

        if (containers.Count == 0)
            return;

        if (containers.All(c => c.ContainedEntity == null))
        {
            _popup.PopupClient("There are no visors to swap to currently.", ent, args.Performer, PopupType.SmallCaution);
            return;
        }

        ref var current = ref ent.Comp.CurrentVisor;
        if (current != null &&
            containers.TryGetValue(current.Value, out var currentContainer) &&
            currentContainer.ContainedEntity is { } currentContained)
        {
            var ev = new DeactivateVisorEvent(ent);
            RaiseLocalEvent(currentContained, ref ev);
        }

        current = current == null ? 0 : current + 1;
        Dirty(ent);

        if (current >= containers.Count)
            current = null;

        if (current != null &&
            containers.TryGetValue(current.Value, out currentContainer) &&
            currentContainer.ContainedEntity is { } newContained)
        {
            var ev = new ActivateVisorEvent(ent, args.Performer);
            RaiseLocalEvent(newContained, ref ev);
        }
    }

    private void OnCycleableVisorInteractUsing(Entity<CycleableVisorComponent> ent, ref InteractUsingEvent args)
    {
        if (TryComp(args.Used, out VisorComponent? visor))
        {
            if (AttachVisor(ent, (args.Used, visor), args.User))
                args.Handled = true;

            return;
        }

        foreach (var tool in ent.Comp.RemoveQuality)
        {
            if (!_tool.HasQuality(args.Used, tool))
                return;
        }

        args.Handled = true;

        if (ent.Comp.CurrentVisor != null &&
            ent.Comp.Containers.TryGetValue(ent.Comp.CurrentVisor.Value, out var currentId) &&
            _container.TryGetContainer(ent, currentId, out var currentContainer))
        {
            foreach (var contained in currentContainer.ContainedEntities)
            {
                var ev = new DeactivateVisorEvent(ent);
                RaiseLocalEvent(contained, ref ev);
            }
        }

        var anyRemoved = false;
        foreach (var id in ent.Comp.Containers)
        {
            if (!_container.TryGetContainer(ent, id, out var container))
                continue;

            if (_container.EmptyContainer(container).Count > 0)
                anyRemoved = true;
        }

        if (anyRemoved)
            _popup.PopupClient("You remove the inserted visors", args.Target, args.User);

        ent.Comp.CurrentVisor = null;
        Dirty(ent);
    }

    private void OnVisorActivate(Entity<VisorComponent> ent, ref ActivateVisorEvent args)
    {
        _powerCell.SetDrawEnabled(ent.Owner, true);
    }

    private void OnVisorDeactivate(Entity<VisorComponent> ent, ref DeactivateVisorEvent args)
    {
        _powerCell.SetDrawEnabled(ent.Owner, false);
    }

    private void OnCycleableVisorPowerCellChanged(Entity<VisorComponent> ent, ref PowerCellChangedEvent args)
    {
        if (!args.Ejected && _powerCell.HasActivatableCharge(ent))
            return;

        if (!_container.TryGetContainingContainer((ent, null), out var visorContainer) ||
            !TryComp(visorContainer.Owner, out CycleableVisorComponent? cycleable))
        {
            return;
        }

        var ev = new DeactivateVisorEvent((visorContainer.Owner, cycleable));
        RaiseLocalEvent(ent, ref ev);
    }

    private bool AttachVisor(Entity<CycleableVisorComponent> cycleable,
        Entity<VisorComponent> visor,
        EntityUid user)
    {
        if (!HasComp<ItemComponent>(visor))
            return false;

        string msg;
        foreach (var id in cycleable.Comp.Containers)
        {
            var container = _container.EnsureContainer<ContainerSlot>(cycleable, id);
            if (_container.Insert(visor.Owner, container))
            {
                msg = $"You connect the {Name(visor)} to {Name(cycleable)}.";
                _popup.PopupClient(msg, cycleable, user);

                if (visor.Comp.Add != null)
                    EntityManager.AddComponents(cycleable, visor.Comp.Add);

                return true;
            }
        }

        msg = $"{Name(cycleable)} has used all of its visor attachment sockets.";
        _popup.PopupClient(msg, cycleable, user, PopupType.SmallCaution);
        return true;
    }
}
