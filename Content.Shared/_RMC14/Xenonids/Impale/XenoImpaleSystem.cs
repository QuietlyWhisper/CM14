using Content.Shared._RMC14.Emote;
using Content.Shared._RMC14.Xenonids.Finesse;
using Content.Shared.Damage;
using Content.Shared.Effects;
using Content.Shared.FixedPoint;
using Content.Shared.Coordinates;
using Robust.Shared.Timing;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Network;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Content.Shared._RMC14.Weapons.Melee;

namespace Content.Shared._RMC14.Xenonids.Impale;

public sealed class XenoImpaleSystem : EntitySystem
{
    [Dependency] private readonly INetManager _net = default!;
    [Dependency] private readonly SharedRMCEmoteSystem _emote = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly SharedColorFlashEffectSystem _flash = default!;
    [Dependency] private readonly DamageableSystem _damage = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly SharedRMCMeleeWeaponSystem _rmcMelee = default!;


    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<XenoImpaleComponent, XenoImpaleActionEvent>(OnXenoImpaleAction);
    }

    private void OnXenoImpaleAction(Entity<XenoImpaleComponent> xeno, ref XenoImpaleActionEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;

        if (HasComp<XenoMarkedComponent>(args.Target))
        {
            if (xeno.Comp.Emote is { } emote)
                _emote.TryEmoteWithChat(xeno, emote, cooldown: xeno.Comp.EmoteCooldown);

            var secondHit = EnsureComp<XenoSecondImpaleComponent>(args.Target);
            secondHit.Damage = xeno.Comp.Damage;
            secondHit.ImpaleAt = _timing.CurTime + xeno.Comp.SecondImpaleTime;
            secondHit.Origin = xeno;

            RemCompDeferred<XenoMarkedComponent>(args.Target);
        }

        Impale(xeno.Comp.Damage, xeno.Comp.AP, xeno.Comp.Animation, xeno.Comp.Sound, args.Target, xeno);

    }

    private void Impale(DamageSpecifier damage, int aP, EntProtoId animation, SoundSpecifier sound, EntityUid target, EntityUid xeno)
    {
        //TODO RMC14 targets chest
        var damageTaken = _damage.TryChangeDamage(target, damage, armorPiercing: aP);
        if (damageTaken?.GetTotal() > FixedPoint2.Zero)
        {
            var filter = Filter.Pvs(target, entityManager: EntityManager).RemoveWhereAttachedEntity(o => o == xeno);
            _flash.RaiseEffect(Color.Red, new List<EntityUid> { target }, filter);
        }

        _rmcMelee.DoLunge(xeno, target);

        if (_net.IsClient)
            return;

        _audio.PlayPvs(sound, xeno);
        SpawnAttachedTo(animation, target.ToCoordinates());
    }

    public override void Update(float frameTime)
    {
        if (_net.IsClient)
            return;

        var time = _timing.CurTime;

        var impaleQuery = EntityQueryEnumerator<XenoSecondImpaleComponent>();

        while (impaleQuery.MoveNext(out var uid, out var impale))
        {
            if (impale.ImpaleAt > time)
                continue;

            Impale(impale.Damage, impale.AP, impale.Animation, impale.Sound, uid, impale.Origin);
            RemCompDeferred<XenoSecondImpaleComponent>(uid);
        }
    }
}
