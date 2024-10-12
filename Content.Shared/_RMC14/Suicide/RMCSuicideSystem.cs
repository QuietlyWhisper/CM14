﻿using Content.Shared.Atmos.Rotting;
using Content.Shared.Damage;
using Content.Shared.DoAfter;
using Content.Shared.Hands.Components;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Systems;
using Content.Shared.Popups;
using Content.Shared.Verbs;
using Content.Shared.Weapons.Ranged;
using Content.Shared.Weapons.Ranged.Components;
using Content.Shared.Weapons.Ranged.Events;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Timing;

namespace Content.Shared._RMC14.Suicide;

// this ignores the suicide cvar since we don't want upstream suicides
public sealed class RMCSuicideSystem : EntitySystem
{
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly DamageableSystem _damageable = default!;
    [Dependency] private readonly SharedDoAfterSystem _doAfter = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly IGameTiming _timing = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<RMCSuicideComponent, GetVerbsEvent<AlternativeVerb>>(OnSuicideGetVerbs);
        SubscribeLocalEvent<RMCSuicideComponent, RMCSuicideDoAfterEvent>(OnSuicideDoAfter);
        SubscribeLocalEvent<RMCHasSuicidedComponent, UpdateMobStateEvent>(OnHasSuicidedUpdateMobState);
    }

    private void OnSuicideGetVerbs(Entity<RMCSuicideComponent> ent, ref GetVerbsEvent<AlternativeVerb> args)
    {
        if (!args.CanInteract)
            return;

        var user = args.User;
        if (user != args.Target || args.Hands is not { } hands)
            return;

        var hasGun = false;
        foreach (var hand in hands.Hands.Values)
        {
            if (HasComp<GunComponent>(hand.HeldEntity))
            {
                hasGun = true;
                break;
            }
        }

        if (!hasGun)
            return;

        args.Verbs.Add(new AlternativeVerb
        {
            Text = Loc.GetString("rmc-suicide"),
            Act = () =>
            {
                var time = _timing.CurTime;
                if (time < ent.Comp.LastAttempt + ent.Comp.Cooldown)
                {
                    _popup.PopupClient(Loc.GetString("rmc-suicide-fumble-self"), user, user, PopupType.SmallCaution);
                    return;
                }

                ent.Comp.LastAttempt = time;

                var ev = new RMCSuicideDoAfterEvent();
                var doAfter = new DoAfterArgs(EntityManager, user, ent.Comp.Delay, ev, user)
                {
                    BreakOnMove = true,
                    NeedHand = true,
                    BreakOnHandChange = true,
                };

                if (_doAfter.TryStartDoAfter(doAfter))
                {
                    var selfMsg = Loc.GetString("rmc-suicide-start-self");
                    var othersMsg = Loc.GetString("rmc-suicide-start-others", ("user", user));
                    _popup.PopupPredicted(selfMsg, othersMsg, user, user, PopupType.LargeCaution);
                }
            },
        });
    }

    private void OnSuicideDoAfter(Entity<RMCSuicideComponent> ent, ref RMCSuicideDoAfterEvent args)
    {
        var user = args.User;
        if (args.Cancelled)
        {
            var selfMsg = Loc.GetString("rmc-suicide-cancel-self");
            var othersMsg = Loc.GetString("rmc-suicide-cancel-others", ("user", user));
            _popup.PopupPredicted(selfMsg, othersMsg, user, user, PopupType.MediumCaution);
            return;
        }

        if (args.Handled)
            return;

        args.Handled = true;

        if (!TryComp(user, out HandsComponent? hands) ||
            hands.ActiveHand?.HeldEntity is not { } held ||
            !TryComp(held, out GunComponent? gun))
        {
            return;
        }

        var ammo = new List<(EntityUid? Entity, IShootable Shootable)>();
        var ev = new TakeAmmoEvent(1, ammo, Transform(user).Coordinates, user);
        RaiseLocalEvent(held, ev);

        if (ev.Ammo.Count == 0)
        {
            _audio.PlayPredicted(gun.SoundEmpty, held, ent);
            return;
        }

        foreach (var (bullet, _) in ev.Ammo)
        {
            QueueDel(bullet);
        }

        _damageable.TryChangeDamage(user, ent.Comp.Damage, true);
        _mobState.ChangeMobState(user, MobState.Dead);
        EnsureComp<RMCHasSuicidedComponent>(user);
        EnsureComp<RottingComponent>(user);
        _audio.PlayPredicted(gun.SoundGunshot, held, ent);
    }

    private void OnHasSuicidedUpdateMobState(Entity<RMCHasSuicidedComponent> ent, ref UpdateMobStateEvent args)
    {
        args.State = MobState.Dead;
    }
}
