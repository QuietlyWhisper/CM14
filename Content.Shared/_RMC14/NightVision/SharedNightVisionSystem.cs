using Content.Shared._RMC14.Marines.Skills;
using Content.Shared._RMC14.Scoping;
using Content.Shared._RMC14.Visor;
using Content.Shared.Actions;
using Content.Shared.Alert;
using Content.Shared.IgnitionSource;
using Content.Shared.Inventory;
using Content.Shared.Inventory.Events;
using Content.Shared.Popups;
using Content.Shared.Rounding;
using Content.Shared.Toggleable;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Timing;

namespace Content.Shared._RMC14.NightVision;

public abstract class SharedNightVisionSystem : EntitySystem
{
    [Dependency] private readonly SharedActionsSystem _actions = default!;
    [Dependency] private readonly AlertsSystem _alerts = default!;
    [Dependency] private readonly SharedAppearanceSystem _appearance = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly InventorySystem _inventory = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly SkillsSystem _skills = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly VisorSystem _visor = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<NightVisionComponent, ComponentStartup>(OnNightVisionStartup);
        SubscribeLocalEvent<NightVisionComponent, MapInitEvent>(OnNightVisionMapInit);
        SubscribeLocalEvent<NightVisionComponent, AfterAutoHandleStateEvent>(OnNightVisionAfterHandle);
        SubscribeLocalEvent<NightVisionComponent, ComponentRemove>(OnNightVisionRemove);
        SubscribeLocalEvent<NightVisionComponent, ToggleNightVisionAlertEvent>(OnNightVisionToggle);

        SubscribeLocalEvent<NightVisionItemComponent, GetItemActionsEvent>(OnNightVisionItemGetActions);
        SubscribeLocalEvent<NightVisionItemComponent, ToggleActionEvent>(OnNightVisionItemToggle);
        SubscribeLocalEvent<NightVisionItemComponent, GotEquippedEvent>(OnNightVisionItemGotEquipped);
        SubscribeLocalEvent<NightVisionItemComponent, GotUnequippedEvent>(OnNightVisionItemGotUnequipped);
        SubscribeLocalEvent<NightVisionItemComponent, ActionRemovedEvent>(OnNightVisionItemActionRemoved);
        SubscribeLocalEvent<NightVisionItemComponent, ComponentRemove>(OnNightVisionItemRemove);
        SubscribeLocalEvent<NightVisionItemComponent, EntityTerminatingEvent>(OnNightVisionItemTerminating);

        SubscribeLocalEvent<NightVisionVisorComponent, ActivateVisorEvent>(OnNightVisionActivate);
        SubscribeLocalEvent<NightVisionVisorComponent, DeactivateVisorEvent>(OnNightVisionDeactivate);
        SubscribeLocalEvent<NightVisionVisorComponent, VisorRelayedEvent<ScopedEvent>>(OnNightVisionScoped);

        SubscribeLocalEvent<RMCNightVisionVisibleOnIgniteComponent, IgnitionEvent>(OnNightVisionVisibleIgnition);
    }

    private void OnNightVisionStartup(Entity<NightVisionComponent> ent, ref ComponentStartup args)
    {
        NightVisionChanged(ent);
    }

    private void OnNightVisionAfterHandle(Entity<NightVisionComponent> ent, ref AfterAutoHandleStateEvent args)
    {
        NightVisionChanged(ent);
    }

    private void OnNightVisionMapInit(Entity<NightVisionComponent> ent, ref MapInitEvent args)
    {
        UpdateAlert(ent);
    }

    private void OnNightVisionRemove(Entity<NightVisionComponent> ent, ref ComponentRemove args)
    {
        if (ent.Comp.Alert is { } alert)
            _alerts.ClearAlert(ent, alert);

        NightVisionRemoved(ent);
    }

    private void OnNightVisionToggle(Entity<NightVisionComponent> ent, ref ToggleNightVisionAlertEvent args)
    {
        Toggle((ent, ent));
    }

    private void OnNightVisionItemGetActions(Entity<NightVisionItemComponent> ent, ref GetItemActionsEvent args)
    {
        if (ent.Comp.ActionId == null)
            return;

        if (args.InHands || !ent.Comp.Toggleable)
            return;

        if (ent.Comp.SlotFlags != args.SlotFlags)
            return;

        args.AddAction(ref ent.Comp.Action, ent.Comp.ActionId);
    }

    private void OnNightVisionItemToggle(Entity<NightVisionItemComponent> ent, ref ToggleActionEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;
        ToggleNightVisionItem(ent, args.Performer);
    }

    private void OnNightVisionItemGotEquipped(Entity<NightVisionItemComponent> ent, ref GotEquippedEvent args)
    {
        if (ent.Comp.SlotFlags != args.SlotFlags)
            return;

        EnableNightVisionItem(ent, args.Equipee);
    }

    private void OnNightVisionItemGotUnequipped(Entity<NightVisionItemComponent> ent, ref GotUnequippedEvent args)
    {
        if (ent.Comp.SlotFlags != args.SlotFlags)
            return;

        DisableNightVisionItem(ent, args.Equipee);
    }

    private void OnNightVisionItemActionRemoved(Entity<NightVisionItemComponent> ent, ref ActionRemovedEvent args)
    {
        DisableNightVisionItem(ent, ent.Comp.User);
    }

    private void OnNightVisionItemRemove(Entity<NightVisionItemComponent> ent, ref ComponentRemove args)
    {
        DisableNightVisionItem(ent, ent.Comp.User);
    }

    private void OnNightVisionItemTerminating(Entity<NightVisionItemComponent> ent, ref EntityTerminatingEvent args)
    {
        DisableNightVisionItem(ent, ent.Comp.User);
    }

    private void OnNightVisionActivate(Entity<NightVisionVisorComponent> ent, ref ActivateVisorEvent args)
    {
        if (HasComp<ScopingComponent>(args.User))
        {
            _popup.PopupClient("You cannot use the night vision optic while using optics.",
                args.User,
                args.User,
                PopupType.SmallCaution);
            return;
        }

        args.Handled = true;
        if (_timing.ApplyingState)
            return;

        var comp = new NightVisionItemComponent()
        {
            ActionId = null,
            SlotFlags = SlotFlags.HEAD,
            Green = true,
            BlockScopes = true,
        };
        AddComp(args.CycleableVisor, comp, true);
        Dirty(args.CycleableVisor, comp);

        if (_inventory.InSlotWithFlags(args.CycleableVisor.Owner, comp.SlotFlags))
        {
            EnableNightVisionItem((args.CycleableVisor, comp), args.User);
            _audio.PlayLocal(ent.Comp.SoundOn, ent, args.User);
        }
    }

    private void OnNightVisionDeactivate(Entity<NightVisionVisorComponent> ent, ref DeactivateVisorEvent args)
    {
        if (_timing.ApplyingState)
            return;

        if (TerminatingOrDeleted(ent))
            return;

        RemComp<NightVisionItemComponent>(args.CycleableVisor);
        _audio.PlayLocal(ent.Comp.SoundOff, ent, args.User);
    }

    private void OnNightVisionScoped(Entity<NightVisionVisorComponent> ent, ref VisorRelayedEvent<ScopedEvent> args)
    {
        _visor.DeactivateVisor(args.CycleableVisor, ent.Owner, args.Event.User);
    }

    private void OnNightVisionVisibleIgnition(Entity<RMCNightVisionVisibleOnIgniteComponent> ent, ref IgnitionEvent args)
    {
        if (_timing.ApplyingState)
            return;

        if (args.Ignite)
            EnsureComp<RMCNightVisionVisibleComponent>(ent);
        else
            RemCompDeferred<RMCNightVisionVisibleComponent>(ent);
    }

    public NightVisionState Toggle(Entity<NightVisionComponent?> ent)
    {
        if (!Resolve(ent, ref ent.Comp))
            return NightVisionState.Off;

        ent.Comp.State = ent.Comp.State switch
        {
            NightVisionState.Off => NightVisionState.Half,
            NightVisionState.Half => NightVisionState.Full,
            NightVisionState.Full => NightVisionState.Off,
            _ => throw new ArgumentOutOfRangeException(),
        };

        Dirty(ent);
        UpdateAlert((ent, ent.Comp));
        return ent.Comp.State;
    }

    private void UpdateAlert(Entity<NightVisionComponent> ent)
    {
        if (ent.Comp.Alert is { } alert)
        {
            var level = MathF.Max((int) NightVisionState.Off, (int) ent.Comp.State);
            var max = _alerts.GetMaxSeverity(alert);
            var severity = max - ContentHelpers.RoundToLevels(level, (int) NightVisionState.Full, max + 1);
            _alerts.ShowAlert(ent, alert, (short) severity);
        }

        NightVisionChanged(ent);
    }

    private void ToggleNightVisionItem(Entity<NightVisionItemComponent> item, EntityUid user)
    {
        if (item.Comp.User == user && item.Comp.Toggleable)
        {
            DisableNightVisionItem(item, item.Comp.User);
            _audio.PlayLocal(item.Comp.SoundOff, item.Owner, user);
            return;
        }

        EnableNightVisionItem(item, user);
        _audio.PlayLocal(item.Comp.SoundOn, item.Owner, user);
    }

    private void EnableNightVisionItem(Entity<NightVisionItemComponent> item, EntityUid user)
    {
        DisableNightVisionItem(item, item.Comp.User);

        if (item.Comp.Skills != null && !_skills.HasAllSkills(user, item.Comp.Skills))
        {
            _popup.PopupClient(Loc.GetString("rmc-skills-hud-toggle"), user, user, PopupType.MediumCaution);
            return;
        }

        item.Comp.User = user;
        Dirty(item);

        _appearance.SetData(item, NightVisionItemVisuals.Active, true);

        if (!_timing.ApplyingState)
        {
            if (TryComp(user, out NightVisionComponent? nightVision))
            {
                nightVision = EnsureComp<NightVisionComponent>(user);
                nightVision.State = NightVisionState.Full;
                nightVision.Green = item.Comp.Green;
                nightVision.Mesons = item.Comp.Mesons;
                nightVision.BlockScopes = item.Comp.BlockScopes;
                Dirty(user, nightVision);
            }
            else
            {
                nightVision = new NightVisionComponent()
                {
                    State = NightVisionState.Full,
                    Green = item.Comp.Green,
                    Mesons = item.Comp.Mesons,
                    BlockScopes = item.Comp.BlockScopes,
                };

                AddComp(user, nightVision, true);
                Dirty(user, nightVision);
            }
        }

        _actions.SetToggled(item.Comp.Action, true);
    }

    protected virtual void NightVisionChanged(Entity<NightVisionComponent> ent)
    {
    }

    protected virtual void NightVisionRemoved(Entity<NightVisionComponent> ent)
    {
    }

    public void DisableNightVisionItem(Entity<NightVisionItemComponent> item, EntityUid? user)
    {
        _actions.SetToggled(item.Comp.Action, false);

        item.Comp.User = null;
        Dirty(item);

        _appearance.SetData(item, NightVisionItemVisuals.Active, false);

        if (TryComp(user, out NightVisionComponent? nightVision) &&
            !nightVision.Innate)
        {
            RemCompDeferred<NightVisionComponent>(user.Value);
        }
    }

    public void SetSeeThroughContainers(Entity<NightVisionComponent?> ent, bool see)
    {
        if (!Resolve(ent, ref ent.Comp, false))
            return;

        ent.Comp.SeeThroughContainers = see;
        Dirty(ent);
    }
}
