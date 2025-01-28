using Content.Shared._RMC14.Armor;
using Content.Shared._RMC14.Xenonids.Plasma;
using Content.Shared._RMC14.Weapons.Melee;
using Content.Shared.Damage;
using Content.Shared.Popups;
using Content.Shared.Weapons.Melee.Events;
using Robust.Shared.Network;
using Robust.Shared.Timing;
using Content.Shared._RMC14.Slow;

namespace Content.Shared._RMC14.Xenonids.Crippling;

public sealed class XenoCripplingStrikeSystem : EntitySystem
{
    [Dependency] private readonly RMCSlowSystem _slow = default!;
    [Dependency] private readonly INetManager _net = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly XenoSystem _xeno = default!;
    [Dependency] private readonly SharedRMCMeleeWeaponSystem _rmcMelee = default!;
    [Dependency] private readonly XenoPlasmaSystem _xenoPlasma = default!;
    [Dependency] private readonly DamageableSystem _damageable = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<XenoCripplingStrikeComponent, XenoCripplingStrikeActionEvent>(OnXenoCripplingStrikeAction);

        SubscribeLocalEvent<XenoActiveCripplingStrikeComponent, MeleeHitEvent>(OnXenoCripplingStrikeHit);

        SubscribeLocalEvent<VictimCripplingStrikeDamageComponent, DamageModifyEvent>(OnVictimCripplingModify, before: [typeof(CMArmorSystem)]);
    }

    private void OnXenoCripplingStrikeAction(Entity<XenoCripplingStrikeComponent> xeno, ref XenoCripplingStrikeActionEvent args)
    {
        args.Handled = true;
        var active = EnsureComp<XenoActiveCripplingStrikeComponent>(xeno);
        var reset = EnsureComp<MeleeResetComponent>(xeno);
        _rmcMelee.MeleeResetInit((xeno.Owner, reset));

        active.ExpireAt = _timing.CurTime + xeno.Comp.ActiveDuration;
        active.SlowDuration = xeno.Comp.SlowDuration;
        active.DamageMult = xeno.Comp.DamageMult;

        Dirty(xeno, active);

        _popup.PopupClient(Loc.GetString("cm-xeno-crippling-strike-activate"), xeno, xeno);
    }

    private void OnXenoCripplingStrikeHit(Entity<XenoActiveCripplingStrikeComponent> xeno, ref MeleeHitEvent args)
    {
        if (!args.IsHit || args.HitEntities.Count == 0)
            return;

        foreach (var entity in args.HitEntities)
        {
            if (!_xeno.CanAbilityAttackTarget(xeno, entity) ||
                HasComp<VictimCripplingStrikeDamageComponent>(entity))
            {
                continue;
            }

            var victim = EnsureComp<VictimCripplingStrikeDamageComponent>(entity);

            victim.DamageMult = xeno.Comp.DamageMult;

            Dirty(entity, victim);

            _slow.TrySlowdown(entity, xeno.Comp.SlowDuration, ignoreDurationModifier: true);

            var message = Loc.GetString("cm-xeno-crippling-strike-hit", ("target", entity));

            if (_net.IsServer)
                _popup.PopupEntity(message, entity, xeno);

            RemCompDeferred<XenoActiveCripplingStrikeComponent>(xeno);
            break;
        }
    }

    private void OnVictimCripplingModify(Entity<VictimCripplingStrikeDamageComponent> victim, ref DamageModifyEvent args)
    {
        args.Damage *= victim.Comp.DamageMult;
        RemCompDeferred<VictimCripplingStrikeDamageComponent>(victim);
    }
}
