using Content.Shared.Whitelist;
using Content.Shared.Actions;
using Content.Shared.DoAfter;
using Content.Shared.Inventory;
using Content.Shared.Item.ItemToggle;
using Content.Shared.Popups;
using Content.Shared.Timing;
using Content.Shared._RMC14.Stealth;
using Content.Shared.Weapons.Ranged.Systems;
using Content.Shared.Movement.Events;
using Content.Shared.Item.ItemToggle.Components;
using Content.Shared._RMC14.NightVision;
using Content.Shared._RMC14.Weapons.Ranged.IFF;
using Robust.Shared.Timing;
using Content.Shared.Inventory.Events;
using Content.Shared._RMC14.Chemistry;
using Content.Shared.Weapons.Ranged.Components;

namespace Content.Shared._RMC14.Armor.Ghillie;

/// <summary>
/// Handles the ghillie suit's prepare position ability.
/// </summary>
public abstract class SharedGhillieSuitSystem : EntitySystem
{
    [Dependency] private readonly SharedDoAfterSystem _doAfter = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly EntityWhitelistSystem _whitelist = default!;
    [Dependency] private readonly InventorySystem _inventory = default!;
    [Dependency] private readonly IGameTiming _timing = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<GhillieSuitComponent, GetItemActionsEvent>(OnGetItemActions);
        SubscribeLocalEvent<GhillieSuitComponent, GhillieSuitPreparePositionActionEvent>(OnPreparePositionAction);
        SubscribeLocalEvent<GhillieSuitComponent, GhillieSuitDoAfterEvent>(OnDoAfter);

        SubscribeLocalEvent<GhillieSuitComponent, GotEquippedEvent>(OnEquipped);
        SubscribeLocalEvent<GhillieSuitComponent, GotUnequippedEvent>(OnUnequipped);

        SubscribeLocalEvent<RMCPassiveStealthComponent, VaporHitEvent>(OnVaporHit);
        SubscribeLocalEvent<RMCPassiveStealthComponent, MoveInputEvent>(OnMove);

        SubscribeLocalEvent<GunComponent, GunShotEvent>(OnGunShot);
    }

    private void OnGetItemActions(Entity<GhillieSuitComponent> ent, ref GetItemActionsEvent args)
    {
        var comp = ent.Comp;

        if (args.InHands || !_inventory.InSlotWithFlags((ent, null, null), SlotFlags.OUTERCLOTHING))
            return;

        args.AddAction(ref comp.Action, comp.ActionId);
        Dirty(ent);
    }

    private void OnPreparePositionAction(Entity<GhillieSuitComponent> ent, ref GhillieSuitPreparePositionActionEvent args)
    {
        var user = args.Performer;
        var comp = ent.Comp;

        if (args.Handled)
            return;

        if (!_whitelist.IsValid(ent.Comp.Whitelist, args.Performer))
        {
            var popup = Loc.GetString("cm-gun-unskilled", ("gun", ent.Owner));
            _popup.PopupClient(popup, args.Performer, args.Performer, PopupType.SmallCaution);
            return;
        }

        args.Handled = true;

        if (!comp.Enabled)
        {
            var ev = new GhillieSuitDoAfterEvent();
            var doAfterEventArgs = new DoAfterArgs(EntityManager, user, comp.UseDelay, ev, ent.Owner)
            {
                BreakOnMove = true,
                BreakOnDamage = true,
                CancelDuplicate = true,
                DuplicateCondition = DuplicateConditions.SameTool
            };

            if (_doAfter.TryStartDoAfter(doAfterEventArgs))
            {
                var activatedPopupSelf = Loc.GetString("rmc-ghillie-activate-self");
                var activatedPopupOthers = Loc.GetString("rmc-ghillie-activate-others", ("user", user));
                _popup.PopupPredicted(activatedPopupSelf, activatedPopupOthers, user, user, PopupType.Medium);
            }
        }
        else
        {
            ToggleInvisibility(ent, user, false);
        }
    }

    private void OnDoAfter(Entity<GhillieSuitComponent> ent, ref GhillieSuitDoAfterEvent args)
    {
        var user = args.User;

        if (args.Cancelled)
            return;

        if (args.Handled)
            return;

        args.Handled = true;
        ToggleInvisibility(ent, user, true);
    }

    private void OnEquipped(Entity<GhillieSuitComponent> ent, ref GotEquippedEvent args)
    {
        if (_timing.ApplyingState)
            return;

        if (!_inventory.InSlotWithFlags((ent, null, null), SlotFlags.OUTERCLOTHING))
            return;

        var comp = EnsureComp<EntityTurnInvisibleComponent>(args.Equipee);
        Dirty(args.Equipee, comp);
    }

    private void OnUnequipped(Entity<GhillieSuitComponent> ent, ref GotUnequippedEvent args)
    {
        if (_timing.ApplyingState)
            return;

        if (_inventory.InSlotWithFlags((ent, null, null), SlotFlags.OUTERCLOTHING))
            return;

        RemCompDeferred<EntityTurnInvisibleComponent>(args.Equipee);
        ToggleInvisibility(ent, args.Equipee, false);
    }

    public void ToggleInvisibility(Entity<GhillieSuitComponent> ent, EntityUid user, bool enabling)
    {
        var comp = ent.Comp;

        if (!TryComp<EntityTurnInvisibleComponent>(user, out var turnInvisible))
            return;

        if (enabling && !HasComp<EntityActiveInvisibleComponent>(user))
        {
            turnInvisible.Enabled = true;

            comp.Enabled = true;
            Dirty(ent);

            var passiveInvisibility = EnsureComp<RMCPassiveStealthComponent>(user);
            passiveInvisibility.MinOpacity = comp.Opacity;
            passiveInvisibility.Delay = comp.InvisibilityDelay;
            passiveInvisibility.Enabled = true;
            passiveInvisibility.ToggleTime = _timing.CurTime;
            Dirty(user, passiveInvisibility);

            var activeInvisibility = EnsureComp<EntityActiveInvisibleComponent>(user);
            activeInvisibility.Opacity = 1f;
            Dirty(user, activeInvisibility);

            turnInvisible.UncloakTime = _timing.CurTime;
            Dirty(user, turnInvisible);

            EnsureComp<EntityIFFComponent>(user);
            RemCompDeferred<RMCNightVisionVisibleComponent>(user);
        }
        else
        {
            turnInvisible.Enabled = false;

            comp.Enabled = false;
            Dirty(ent);

            EnsureComp<RMCNightVisionVisibleComponent>(user);

            RemCompDeferred<RMCPassiveStealthComponent>(user);
            RemCompDeferred<EntityActiveInvisibleComponent>(user);
            RemCompDeferred<EntityIFFComponent>(user);

            turnInvisible.UncloakTime = _timing.CurTime;
            Dirty(user, turnInvisible);

            var deactivatedPopupSelf = Loc.GetString("rmc-ghillie-fail-self");
            var deactivatedPopupOthers = Loc.GetString("rmc-ghillie-fail-others", ("user", user));
            _popup.PopupPredicted(deactivatedPopupSelf, deactivatedPopupOthers, user, user, PopupType.MediumCaution);
        }
    }

    /// <summary>
    /// Finds a ghillie suit on a user.
    /// </summary>
    public Entity<GhillieSuitComponent>? FindSuit(EntityUid uid)
    {
        var slots = _inventory.GetSlotEnumerator(uid, SlotFlags.OUTERCLOTHING);
        while (slots.MoveNext(out var slot))
        {
            if (TryComp(slot.ContainedEntity, out GhillieSuitComponent? comp))
                return (slot.ContainedEntity.Value, comp);
        }

        return null;
    }

    private void OnVaporHit(Entity<RMCPassiveStealthComponent> ent, ref VaporHitEvent args)
    {
        var user = ent.Owner;
        var suit = FindSuit(user);

        if (suit == null)
            return;

        ToggleInvisibility(suit.Value, user, false);
    }

    private void OnMove(Entity<RMCPassiveStealthComponent> ent, ref MoveInputEvent args)
    {
        var user = ent.Owner;
        var suit = FindSuit(user);

        if (suit == null)
            return;

        ToggleInvisibility(suit.Value, user, false);
    }

    private void OnGunShot(Entity<GunComponent> ent, ref GunShotEvent args)
    {
        var user = args.User;
        var suit = FindSuit(user);

        if (suit == null)
            return;

        if (suit.Value.Comp.Enabled
            && TryComp<EntityActiveInvisibleComponent>(user, out var invis)
            && TryComp<RMCPassiveStealthComponent>(user, out var passive))
        {
            passive.ToggleTime = _timing.CurTime;
            Dirty(user, passive);

            invis.Opacity = 1;
            Dirty(user, invis);

            var deactivatedPopupSelf = Loc.GetString("rmc-ghillie-fail-self");
            var deactivatedPopupOthers = Loc.GetString("rmc-ghillie-fail-others", ("user", user));
            _popup.PopupPredicted(deactivatedPopupSelf, deactivatedPopupOthers, user, user, PopupType.MediumCaution);
        }
    }
}
