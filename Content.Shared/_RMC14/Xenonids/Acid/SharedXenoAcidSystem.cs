﻿using Content.Shared._RMC14.Entrenching;
using Content.Shared._RMC14.Xenonids.Plasma;
using Content.Shared.Coordinates;
using Content.Shared.Damage;
using Content.Shared.Damage.Prototypes;
using Content.Shared.DoAfter;
using Content.Shared.Mobs.Systems;
using Content.Shared.Popups;
using Robust.Shared.Network;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;
using Robust.Shared.Configuration;
using Content.Shared._RMC14.CCVar;

namespace Content.Shared._RMC14.Xenonids.Acid;

public abstract class SharedXenoAcidSystem : EntitySystem
{
    [Dependency] private readonly SharedDoAfterSystem _doAfter = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly INetManager _net = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly XenoPlasmaSystem _xenoPlasma = default!;
    [Dependency] private readonly DamageableSystem _damageable = default!;
    [Dependency] private readonly IPrototypeManager _prototypeManager = default!;
    [Dependency] private readonly IConfigurationManager _config = default!;
    private int _corrosiveAcidTickDelaySeconds;
    private string _corrosiveAcidDamageType = "Heat";
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<XenoAcidComponent, XenoCorrosiveAcidEvent>(OnXenoCorrosiveAcid);
        SubscribeLocalEvent<XenoAcidComponent, DoAfterAttemptEvent<XenoCorrosiveAcidDoAfterEvent>>(OnXenoCorrosiveAcidDoAfterAttempt);
        SubscribeLocalEvent<XenoAcidComponent, XenoCorrosiveAcidDoAfterEvent>(OnXenoCorrosiveAcidDoAfter);

        Subs.CVar(_config, RMCCVars.RMCCorrosiveAcidTickDelaySeconds, obj => _corrosiveAcidTickDelaySeconds = obj, true);
        Subs.CVar(_config, RMCCVars.RMCCorrosiveAcidDamageType, obj => _corrosiveAcidDamageType = obj, true);
    }

    private void OnXenoCorrosiveAcid(Entity<XenoAcidComponent> xeno, ref XenoCorrosiveAcidEvent args)
    {
        if (xeno.Owner != args.Performer ||
            !CheckCorrodiblePopups(xeno, args.Target))
        {
            return;
        }

        var doAfter = new DoAfterArgs(EntityManager, xeno, xeno.Comp.AcidDelay, new XenoCorrosiveAcidDoAfterEvent(args), xeno, args.Target)
        {
            BreakOnMove = true,
            RequireCanInteract = false,
            AttemptFrequency = AttemptFrequency.StartAndEnd
        };
        _doAfter.TryStartDoAfter(doAfter);
    }

    private void OnXenoCorrosiveAcidDoAfterAttempt(Entity<XenoAcidComponent> ent, ref DoAfterAttemptEvent<XenoCorrosiveAcidDoAfterEvent> args)
    {
        if (args.Cancelled)
            return;

        if (_mobState.IsIncapacitated(ent))
            args.Cancel();
    }

    private void OnXenoCorrosiveAcidDoAfter(Entity<XenoAcidComponent> xeno, ref XenoCorrosiveAcidDoAfterEvent args)
    {
        if (args.Handled || args.Cancelled || args.Target is not { } target)
            return;

        if (!CheckCorrodiblePopups(xeno, target))
            return;

        if (!_xenoPlasma.TryRemovePlasmaPopup(xeno.Owner, args.PlasmaCost))
            return;

        if (_net.IsClient)
            return;

        args.Handled = true;

        var acid = SpawnAttachedTo(args.AcidId, target.ToCoordinates());

        var ev = new CorrodingEvent(acid, args.Dps, args.ExpendableLightDps);
        RaiseLocalEvent(target, ref ev);
        if (ev.Cancelled)
            return;

        AddComp(target, new TimedCorrodingComponent
        {
            Acid = acid,
            CorrodesAt = _timing.CurTime + args.Time
        });
    }

    private bool CheckCorrodiblePopups(Entity<XenoAcidComponent> xeno, EntityUid target)
    {
        if (!TryComp(target, out CorrodibleComponent? corrodible) ||
            !corrodible.IsCorrodible)
        {
            _popup.PopupClient(Loc.GetString("cm-xeno-acid-not-corrodible", ("target", target)), xeno, xeno, PopupType.SmallCaution);
            return false;
        }

        if (HasComp<TimedCorrodingComponent>(target))
        {
            _popup.PopupClient(Loc.GetString("cm-xeno-acid-already-corroding", ("target", target)), xeno, xeno);
            return false;
        }

        return true;
    }

    public override void Update(float frameTime)
    {
        if (_net.IsClient)
            return;

        var time = _timing.CurTime;

        var damageableCorrodingQuery = EntityQueryEnumerator<DamageableCorrodingComponent>();
        while (damageableCorrodingQuery.MoveNext(out var uid, out var damageableCorrodingComponent))
        {
            if (time > damageableCorrodingComponent.LastDamagedAt.Add(TimeSpan.FromSeconds(_corrosiveAcidTickDelaySeconds)))
            {
                DamageSpecifier damage = new(_prototypeManager.Index<DamageTypePrototype>(_corrosiveAcidDamageType), damageableCorrodingComponent.Dps * _corrosiveAcidTickDelaySeconds);
                _damageable.TryChangeDamage(uid, damage, true);
                damageableCorrodingComponent.LastDamagedAt = time;
            }
        }

        var timedCorrodingQuery = EntityQueryEnumerator<TimedCorrodingComponent>();
        while (timedCorrodingQuery.MoveNext(out var uid, out var timedCorrodingComponent))
        {
            if (time < timedCorrodingComponent.CorrodesAt)
                continue;

            QueueDel(uid);
            QueueDel(timedCorrodingComponent.Acid);
        }
    }
}