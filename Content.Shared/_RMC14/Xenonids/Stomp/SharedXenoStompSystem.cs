using Content.Shared._RMC14.Marines;
using Content.Shared._RMC14.Slow;
using Content.Shared._RMC14.Standing;
using Content.Shared._RMC14.Xenonids.Plasma;
using Content.Shared.Coordinates;
using Content.Shared.Damage;
using Content.Shared.Effects;
using Content.Shared.FixedPoint;
using Content.Shared.Mobs.Systems;
using Content.Shared.Standing;
using Content.Shared.Stunnable;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Network;
using Robust.Shared.Player;

namespace Content.Shared._RMC14.Xenonids.Stomp;

public sealed class XenoStompSystem : EntitySystem
{
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly SharedColorFlashEffectSystem _colorFlash = default!;
    [Dependency] private readonly DamageableSystem _damageable = default!;
    [Dependency] private readonly XenoPlasmaSystem _xenoPlasma = default!;
    [Dependency] private readonly INetManager _net = default!;
    [Dependency] private readonly EntityLookupSystem _entityLookup = default!;
    [Dependency] private readonly SharedStunSystem _stun = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly StandingStateSystem _standing = default!;
    [Dependency] private readonly RMCSlowSystem _slow = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<XenoStompComponent, XenoStompActionEvent>(OnXenoStompAction);
    }

    private readonly HashSet<Entity<MarineComponent>> _receivers = new();

    private void OnXenoStompAction(Entity<XenoStompComponent> xeno, ref XenoStompActionEvent args)
    {
        var ev = new XenoStompAttemptEvent();
        RaiseLocalEvent(xeno, ref ev);

        if (ev.Cancelled)
            return;

        if (!TryComp(xeno, out TransformComponent? xform) ||
            _mobState.IsDead(xeno))
        {
            return;
        }

        if (!_xenoPlasma.TryRemovePlasmaPopup(xeno.Owner, xeno.Comp.PlasmaCost))
            return;

        args.Handled = true;

        _receivers.Clear();
        _entityLookup.GetEntitiesInRange(xform.Coordinates, xeno.Comp.Range, _receivers);

        if (_net.IsServer)
            _audio.PlayPvs(xeno.Comp.Sound, xeno);

        foreach (var receiver in _receivers)
        {
            if (_mobState.IsDead(receiver))
                continue;

            _stun.TryParalyze(receiver, xeno.Comp.ParalyzeTime, true);
            if (xeno.Comp.Slows)
                _slow.TrySuperSlowdown(receiver, xeno.Comp.SlowTime, true);

            if (xform.Coordinates.TryDistance(EntityManager, receiver.Owner.ToCoordinates(), out var distance) && distance <= xeno.Comp.ShortRange)
            {
                if (!_standing.IsDown(receiver))
                    continue;

                var damage = _damageable.TryChangeDamage(receiver, xeno.Comp.Damage);
                if (damage?.GetTotal() > FixedPoint2.Zero)
                {
                    var filter = Filter.Pvs(receiver, entityManager: EntityManager).RemoveWhereAttachedEntity(o => o == xeno.Owner);
                    _colorFlash.RaiseEffect(Color.Red, new List<EntityUid> { receiver }, filter);
                }
            }
        }

        if (_net.IsServer && xeno.Comp.SelfEffect is not null)
            SpawnAttachedTo(xeno.Comp.SelfEffect, xeno.Owner.ToCoordinates());
    }
}
