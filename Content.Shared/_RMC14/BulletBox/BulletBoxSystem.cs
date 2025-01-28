using Content.Shared.Database;
using Content.Shared.DoAfter;
using Content.Shared.Examine;
using Content.Shared.Interaction;
using Content.Shared.Popups;
using Content.Shared.Verbs;
using Content.Shared.Weapons.Ranged.Components;
using Content.Shared.Weapons.Ranged.Systems;
using Robust.Shared.Network;

namespace Content.Shared._RMC14.BulletBox;

public sealed class BulletBoxSystem : EntitySystem
{
    [Dependency] private readonly SharedAppearanceSystem _appearance = default!;
    [Dependency] private readonly SharedDoAfterSystem _doAfter = default!;
    [Dependency] private readonly SharedGunSystem _gun = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly INetManager _net = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<BulletBoxComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<BulletBoxComponent, ExaminedEvent>(OnExamined);
        SubscribeLocalEvent<BulletBoxComponent, InteractUsingEvent>(OnInteractUsing);
        SubscribeLocalEvent<BulletBoxComponent, BulletBoxTransferDoAfterEvent>(OnTransferDoAfter);
        SubscribeLocalEvent<BulletBoxComponent, GetVerbsEvent<AlternativeVerb>>(OnGetAlternativeVerbs);
    }

    private void OnMapInit(Entity<BulletBoxComponent> ent, ref MapInitEvent args)
    {
        UpdateAppearance(ent);
    }

    private void OnExamined(Entity<BulletBoxComponent> ent, ref ExaminedEvent args)
    {
        using (args.PushGroup(nameof(BulletBoxComponent)))
        {
            args.PushText(Loc.GetString("rmc-bullet-box-amount", ("amount", ent.Comp.Amount)));
        }
    }

    private void OnGetAlternativeVerbs(Entity<BulletBoxComponent> ent, ref GetVerbsEvent<AlternativeVerb> args)
    {
        var user = args.User;
        var verb = new AlternativeVerb()
        {
            Act = () => { ent.Comp.TransferToBox = !ent.Comp.TransferToBox;
                if (_net.IsServer)
                {
                    _popup.PopupEntity(Loc.GetString("rmc-bullet-box-refilling-" + ent.Comp.TransferToBox.ToString()), ent, user);
                }
            },
            Impact = LogImpact.Low,
            Text = Loc.GetString("rmc-bullet-box-toggle")
        };
        args.Verbs.Add(verb);
    }

    private void OnInteractUsing(Entity<BulletBoxComponent> ent, ref InteractUsingEvent args)
    {
        var used = new Entity<RefillableByBulletBoxComponent?, BallisticAmmoProviderComponent?>(args.Used, null, null);
        if (!Resolve(used, ref used.Comp1, ref used.Comp2, false))
            return;

        args.Handled = true;
        var user = args.User;
        if (!CanTransferPopup(ent, user, ref used))
            return;

        var ev = new BulletBoxTransferDoAfterEvent();
        var delay = ent.Comp.DelayTransferFromBox;
        if (ent.Comp.TransferToBox)
        {
            delay = ent.Comp.DelayTransferToBox;
        }
        var doAfter = new DoAfterArgs(EntityManager, user, delay, ev, ent, ent, args.Used)
        {
            BreakOnMove = true,
            BreakOnDropItem = true,
            NeedHand = true,
        };
        _doAfter.TryStartDoAfter(doAfter);
    }

    private void OnTransferDoAfter(Entity<BulletBoxComponent> ent, ref BulletBoxTransferDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled || args.Used is not { } usedId)
            return;

        args.Handled = true;

        var user = args.User;
        var transfer = 0;
        var used = new Entity<RefillableByBulletBoxComponent?, BallisticAmmoProviderComponent?>(usedId, null, null);
        if (!CanTransferPopup(ent, user, ref used) || used.Comp2 == null)
            return;

        if (!ent.Comp.TransferToBox)
        {
            transfer = used.Comp2.Capacity - used.Comp2.Count;
            if (transfer <= 0)
                return;

            transfer = Math.Min(transfer, ent.Comp.Amount);
            _gun.SetBallisticUnspawned((used, used.Comp2), used.Comp2.UnspawnedCount + transfer);
            ent.Comp.Amount -= transfer;
        }
        else
        {
            transfer = ent.Comp.Max - ent.Comp.Amount;
            if (transfer <= 0)
                return;

            transfer = Math.Min(transfer, used.Comp2.Count);
            _gun.SetBallisticUnspawned((used, used.Comp2), used.Comp2.UnspawnedCount - transfer);
            ent.Comp.Amount += transfer;
        }
        if (_net.IsServer)
        {
            _popup.PopupEntity(Loc.GetString("rmc-bullet-box-transfer-done", ("amount", transfer), ("used", ent)), ent, user);
        }
        Dirty(ent);
        UpdateAppearance(ent);
    }

    private bool CanTransferPopup(Entity<BulletBoxComponent> box, EntityUid user, ref Entity<RefillableByBulletBoxComponent?, BallisticAmmoProviderComponent?> used)
    {
        if (!Resolve(used, ref used.Comp1, ref used.Comp2, false))
            return false;

        string? popup = null;

        if (box.Comp.BulletType != used.Comp1.BulletType)
        {
            popup = Loc.GetString("rmc-bullet-box-wrong-rounds");
        }
        if (!box.Comp.TransferToBox)
        {
            if (used.Comp2.Count >= used.Comp2.Capacity)
            {
                popup = Loc.GetString("rmc-bullet-box-mag-full");
            }
            if (box.Comp.Amount <= 0)
            {
                popup = Loc.GetString("rmc-bullet-box-box-empty");
            }
        }
        else
        {
            if (used.Comp2.Count <= 0)
            {
                popup = Loc.GetString("rmc-bullet-box-mag-empty");
            }
            if (box.Comp.Amount >= box.Comp.Max)
            {
                popup = Loc.GetString("rmc-bullet-box-box-full");
            }
        }
        if(popup is not null)
        {
            if (_net.IsServer)
            {
                _popup.PopupEntity(popup, box, user);
            }
            return false;
        }
        return true;
    }

    private void UpdateAppearance(Entity<BulletBoxComponent> ent)
    {
        var visual = ((double) ent.Comp.Amount / ent.Comp.Max) switch
        {
            >= 1 => BulletBoxVisuals.Full,
            >= 0.66 => BulletBoxVisuals.High,
            >= 0.33 => BulletBoxVisuals.Medium,
            > 0 => BulletBoxVisuals.Low,
            _ => BulletBoxVisuals.Empty,
        };

        _appearance.SetData(ent, BulletBoxLayers.Fill, visual);
    }
}
