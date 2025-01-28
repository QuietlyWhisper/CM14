using Content.Shared._RMC14.Weapons.Common;
using Content.Shared.Examine;
using Content.Shared.Popups;
using Content.Shared.Weapons.Ranged.Events;
using Content.Shared.Weapons.Ranged.Systems;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Containers;
using Robust.Shared.Network;

namespace Content.Shared._RMC14.Weapons.Ranged;

public abstract class SharedPumpActionSystem : EntitySystem
{
    [Dependency] private readonly INetManager _net = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<PumpActionComponent, ExaminedEvent>(OnExamined, before: [typeof(SharedGunSystem)]);
        SubscribeLocalEvent<PumpActionComponent, AttemptShootEvent>(OnAttemptShoot);
        SubscribeLocalEvent<PumpActionComponent, GunShotEvent>(OnGunShot);
        SubscribeLocalEvent<PumpActionComponent, UniqueActionEvent>(OnUniqueAction);
        SubscribeLocalEvent<PumpActionComponent, EntRemovedFromContainerMessage>(OnEntRemovedFromContainer);
    }

    protected virtual void OnExamined(Entity<PumpActionComponent> ent, ref ExaminedEvent args)
    {
        // TODO RMC14 the server has no idea what this keybind is supposed to be for the client
        args.PushMarkup(Loc.GetString(ent.Comp.Examine), 1);
    }

    protected virtual void OnAttemptShoot(Entity<PumpActionComponent> ent, ref AttemptShootEvent args)
    {
        if (!ent.Comp.Pumped)
            args.Cancelled = true;
    }

    private void OnGunShot(Entity<PumpActionComponent> ent, ref GunShotEvent args)
    {
        if (ent.Comp.Once)
            return;

        ent.Comp.Pumped = false;
        Dirty(ent);
    }

    private void OnUniqueAction(Entity<PumpActionComponent> ent, ref UniqueActionEvent args)
    {
        if (args.Handled)
            return;

        var ammo = new GetAmmoCountEvent();
        RaiseLocalEvent(ent.Owner, ref ammo);

        if (ammo.Count <= 0)
        {
            _popup.PopupClient(Loc.GetString("cm-gun-no-ammo-message"), args.UserUid, args.UserUid);
            args.Handled = true;
            return;
        }

        if (!ent.Comp.Running || ent.Comp.Pumped)
            return;

        ent.Comp.Pumped = true;
        Dirty(ent);

        args.Handled = true;

        if (_net.IsServer)
            _audio.PlayPvs(ent.Comp.Sound, args.UserUid);
    }

    private void OnEntRemovedFromContainer(Entity<PumpActionComponent> ent, ref EntRemovedFromContainerMessage args)
    {
        if (args.Container.ID != ent.Comp.ContainerId || !ent.Comp.Once)
            return;

        ent.Comp.Pumped = false;
    }
}
