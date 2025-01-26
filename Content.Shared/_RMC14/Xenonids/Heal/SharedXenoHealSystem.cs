using Content.Shared._RMC14.Actions;
using Content.Shared._RMC14.Atmos;
using Content.Shared._RMC14.Damage;
using Content.Shared._RMC14.Stun;
using Content.Shared._RMC14.Xenonids.Announce;
using Content.Shared._RMC14.Xenonids.Construction;
using Content.Shared._RMC14.Xenonids.Energy;
using Content.Shared._RMC14.Xenonids.Hive;
using Content.Shared._RMC14.Xenonids.Parasite;
using Content.Shared._RMC14.Xenonids.Plasma;
using Content.Shared._RMC14.Xenonids.Strain;
using Content.Shared.Coordinates;
using Content.Shared.Damage;
using Content.Shared.Damage.Prototypes;
using Content.Shared.FixedPoint;
using Content.Shared.Interaction;
using Content.Shared.Jittering;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Popups;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Map;
using Robust.Shared.Network;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Shared._RMC14.Xenonids.Heal;

public abstract class SharedXenoHealSystem : EntitySystem
{
    [Dependency] private readonly DamageableSystem _damageable = default!;
    [Dependency] private readonly SharedRMCFlammableSystem _flammable = default!;

    [Dependency] private readonly SharedInteractionSystem _interact = default!;
    [Dependency] private readonly EntityLookupSystem _entityLookup = default!;
    [Dependency] private readonly SharedXenoHiveSystem _hive = default!;
    [Dependency] private readonly XenoStrainSystem _xenoStrain = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly MobThresholdSystem _mobThreshold = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly RMCActionsSystem _rmcActions = default!;
    [Dependency] private readonly SharedRMCDamageableSystem _rmcDamageable = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly XenoPlasmaSystem _xenoPlasma = default!;
    [Dependency] private readonly XenoEnergySystem _xenoEnergy = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly SharedXenoAnnounceSystem _xenoAnnounce = default!;
    [Dependency] private readonly SharedJitteringSystem _jitter = default!;
    [Dependency] private readonly INetManager _net = default!;

    private static readonly ProtoId<DamageGroupPrototype> BruteGroup = "Brute";
    private static readonly ProtoId<DamageGroupPrototype> BurnGroup = "Burn";
    private static readonly ProtoId<DamageTypePrototype> BluntGroup = "Blunt";

    private readonly HashSet<Entity<XenoComponent>> _xenos = new();

    public override void Initialize()
    {
        SubscribeLocalEvent<XenoHealComponent, XenoHealActionEvent>(OnXenoHealAction);
        SubscribeLocalEvent<XenoComponent, XenoApplySalveActionEvent>(OnXenoApplySalveAction);
        SubscribeLocalEvent<XenoComponent, XenoSacrificeHealActionEvent>(OnXenoSacrificeHealAction);
    }

    private void OnXenoHealAction(Entity<XenoHealComponent> ent, ref XenoHealActionEvent args)
    {
        if (args.Handled)
            return;

        if (!_rmcActions.TryUseAction(args.Performer, args.Action))
            return;

        args.Handled = true;

        _xenos.Clear();
        _entityLookup.GetEntitiesInRange(args.Target, ent.Comp.Radius, _xenos);

        if (_xenos.Count == 0)
            return;

        var msg = "We channel our plasma to heal our sisters' wounds around this area.";
        _popup.PopupClient(msg, args.Target, ent, PopupType.Large);

        foreach (var xeno in _xenos)
        {
            if (_mobState.IsDead(xeno) ||
                _flammable.IsOnFire(xeno.Owner))
            {
                continue;
            }

            if (!_mobThreshold.TryGetIncapThreshold(xeno, out var threshold) ||
                threshold <= FixedPoint2.Zero)
            {
                continue;
            }

            if (!_hive.FromSameHive(ent.Owner, xeno.Owner))
                continue;

            var heal = EnsureComp<XenoBeingHealedComponent>(xeno);
            var healStack = new XenoHealStack()
            {
                HealAmount = threshold.Value * ent.Comp.Percentage /
                             (ent.Comp.Duration.TotalSeconds * 10) *
                             (ent.Comp.TimeBetweenHeals.TotalSeconds * 10),
                Charges = (int)(ent.Comp.Duration.TotalSeconds / ent.Comp.TimeBetweenHeals.TotalSeconds),
                TimeBetweenHeals = ent.Comp.TimeBetweenHeals,
            };

            heal.HealStacks.Add(healStack);

            if (_net.IsServer)
                SpawnAttachedTo(ent.Comp.HealEffect, xeno.Owner.ToCoordinates());
        }
    }

    private void OnXenoApplySalveAction(Entity<XenoComponent> ent, ref XenoApplySalveActionEvent args)
    {
        var target = args.Target;

        LocId? failureMessageId = null;

        if (!HasComp<XenoComponent>(target))
            failureMessageId = "rmc-xeno-apply-salve-target-not-xeno-failure";

        if (ent.Owner == target)
            failureMessageId = "rmc-xeno-apply-salve-target-self-failure";

        if (!_hive.FromSameHive(ent.Owner, target))
            failureMessageId = "rmc-xeno-apply-salve-target-hostile-failure";

        if (!_interact.InRangeUnobstructed(ent.Owner, target, args.Range))
            failureMessageId = "rmc-xeno-apply-salve-target-too-far-away-failure";

        if (_mobState.IsDead(target))
            failureMessageId = "rmc-xeno-apply-salve-target-dead-failure";

        if (_flammable.IsOnFire(target))
            failureMessageId = "rmc-xeno-apply-salve-target-on-fire-failure";

        if (TryComp(target, out DamageableComponent? damageComp) && damageComp.TotalDamage == 0)
            failureMessageId = "rmc-xeno-apply-salve-target-full-health-failure";

        if (failureMessageId != null)
        {
            _popup.PopupClient(Loc.GetString(failureMessageId, ("target_xeno", target)), ent);
            return;
        }

        var totalHealAmount = args.StandardHealAmount;
        var damageTakenModifier = args.DamageTakenModifier;
        var healedHealerOrSmallXeno = false;
        if (TryComp(target, out RMCSizeComponent? sizeComp) && (sizeComp.Size == RMCSizes.Small || sizeComp.Size  == RMCSizes.VerySmallXeno))
        {
            totalHealAmount = args.SmallHealAmount;
            damageTakenModifier = 1;
            healedHealerOrSmallXeno = true;
        }

        if (_xenoStrain.AreSameStrain(ent.Owner, target))
        {
            damageTakenModifier = 1;
            healedHealerOrSmallXeno = true;
        }

        if (!_xenoPlasma.TryRemovePlasmaPopup(ent.Owner, (totalHealAmount * args.PlasmaCostModifier)))
        {
            return;
        }

        var damageTaken = totalHealAmount * damageTakenModifier;

        var damageTakenSpecifier = new DamageSpecifier
        {
            DamageDict =
            {
                [BluntGroup] = damageTaken,
            },
        };
        _damageable.TryChangeDamage(ent, damageTakenSpecifier, ignoreResistances: true, interruptsDoAfters: false);
        _popup.PopupClient(Loc.GetString("rmc-xeno-apply-salve-self", ("target_xeno", target)), ent, PopupType.Medium);

        args.Handled = true;

        var heal = EnsureComp<XenoBeingHealedComponent>(target);
        var healStack = new XenoHealStack()
        {
            Charges = (int) (args.TotalHealDuration.TotalSeconds / args.TimeBetweenHeals.TotalSeconds),
            TimeBetweenHeals = args.TimeBetweenHeals,
        };
        healStack.HealAmount = totalHealAmount / healStack.Charges;
        healStack.NextHealAt = _timing.CurTime + healStack.TimeBetweenHeals;
        heal.HealStacks.Add(healStack);
        heal.ParallizeHealing = true;
        var salved = EnsureComp<RecentlySalvedComponent>(ent);
        salved.ExpiresAt = _timing.CurTime + args.TotalHealDuration;

        if(_net.IsServer)
            SpawnAttachedTo(args.HealEffect, target.ToCoordinates());

        _jitter.DoJitter(target, TimeSpan.FromSeconds(1), true, 80, 8, true);

        _audio.PlayPredicted(args.HealSound, target.ToCoordinates(), ent);

        _popup.PopupClient(Loc.GetString("rmc-xeno-apply-salve-target", ("healer_xeno", ent)), target, PopupType.SmallCaution);

        if (!healedHealerOrSmallXeno && TryComp(ent, out XenoEnergyComponent? xenoEnergyComp) && !_xenoEnergy.HasEnergy((ent, xenoEnergyComp), xenoEnergyComp.Max))
        {
            _xenoEnergy.AddEnergy((ent, xenoEnergyComp), (int)damageTaken, false);
            if (_xenoEnergy.HasEnergy((ent, xenoEnergyComp), xenoEnergyComp.Max))
            {
                _popup.PopupClient(Loc.GetString("rmc-xeno-sacrifice-heal-will-respawn"), ent, PopupType.Large);
            }
        }
        Dirty(target, heal);
    }

    private void OnXenoSacrificeHealAction(Entity<XenoComponent> ent, ref XenoSacrificeHealActionEvent args)
    {
        var target = args.Target;

        LocId? failureMessageId = null;

        if (!HasComp<XenoComponent>(target))
            failureMessageId = "rmc-xeno-sacrifice-heal-target-not-xeno-failure";

        if (ent.Owner == target)
            failureMessageId = "rmc-xeno-sacrifice-heal-target-self-failure";

        if (HasComp<XenoParasiteComponent>(target) ||
            (TryComp(target, out RMCSizeComponent? rmcSizeComp) && rmcSizeComp.Size == RMCSizes.VerySmallXeno))
        {
            failureMessageId = "rmc-xeno-sacrifice-heal-target-low-level-failure";
        }

        if (!_hive.FromSameHive(ent.Owner, target))
            failureMessageId = "rmc-xeno-sacrifice-heal-target-hostile-failure";

        if (!_interact.InRangeUnobstructed(ent.Owner, target, args.Range))
            failureMessageId = "rmc-xeno-sacrifice-heal-target-too-far-away-failure";

        if (_mobState.IsDead(target))
            failureMessageId = "rmc-xeno-sacrifice-heal-target-dead-failure";

        if (TryComp(target, out DamageableComponent? targetDamageComp) && targetDamageComp.TotalDamage == 0)
            failureMessageId = "rmc-xeno-sacrifice-heal-target-full-health-failure";

        if (failureMessageId != null)
        {
            _popup.PopupClient(Loc.GetString(failureMessageId, ("target_xeno", target)), ent);
            return;
        }

        if (!TryComp(target, out targetDamageComp) ||
            !TryComp(ent, out DamageableComponent? userDamageComp) ||
            !TryComp(target, out MobThresholdsComponent? targetThresholdsComp) ||
            !TryComp(ent, out MobThresholdsComponent? userThresholdsComp))
        {
            return;
        }

        FixedPoint2? targetCriticalThreshold = null;
        foreach (var threshold in targetThresholdsComp.Thresholds)
        {
            if (threshold.Value == MobState.Critical)
            {
                targetCriticalThreshold = threshold.Key;
            }
        }

        FixedPoint2? userDeathThreshold = null;
        foreach (var threshold in userThresholdsComp.Thresholds)
        {
            if (threshold.Value == MobState.Dead)
            {
                userDeathThreshold = threshold.Key;
            }
        }

        if (userDeathThreshold is null ||
            targetCriticalThreshold is null)
        {
            return;
        }

        SacraficialHealShout(ent);
        _xenoAnnounce.AnnounceSameHive(ent.Owner, Loc.GetString("rmc-xeno-sacrifice-heal-target-announcement", ("healer_xeno", ent), ("target_xeno", target)), popup:PopupType.Large);
        _popup.PopupPredicted(Loc.GetString("rmc-xeno-sacrifice-heal-target-enviorment", ("healer_xeno", ent), ("target_xeno", target)), target, ent, PopupType.Medium);

        // Heal from crit
        var targetTotalDamage = targetDamageComp.TotalDamage;
        var diffToThreshold = targetTotalDamage - targetCriticalThreshold.Value;

        if (diffToThreshold > 0)
            Heal(target, diffToThreshold);

        // Use up user's health to heal target
        var userTotalDamage = userDamageComp.TotalDamage;
        var remainingHealth = userDeathThreshold.Value - userTotalDamage;
        var healAmount = remainingHealth * args.TransferProportion;

        Heal(target, healAmount);

        _jitter.DoJitter(target, TimeSpan.FromSeconds(1), true, 80, 8, true);

        if(_net.IsServer)
         SpawnAttachedTo(args.HealEffect, target.ToCoordinates());

        var corpsePosition = _transform.GetMoverCoordinates(ent);

        // This damage is completely arbitrary, just to ensure the drone dies EVEN WITH WARDING.
        // TODO: Gib the healing xeno here
        var killDamageSpecifier = new DamageSpecifier
        {
            DamageDict =
            {
                [BluntGroup] = remainingHealth * 100 + 3000,
            },
        };
        _damageable.TryChangeDamage(ent, killDamageSpecifier, ignoreResistances: true, interruptsDoAfters: false);

        if (!TryComp(ent, out XenoEnergyComponent? xenoEnergyComp) ||
            !_xenoEnergy.HasEnergy((ent, xenoEnergyComp), xenoEnergyComp.Max))
        {
            return;
        }

        if (GetHiveCore(ent))
            SacraficialHealRespawn(ent, args.RespawnDelay);
        else
            SacraficialHealRespawn(ent, args.RespawnDelay, true, corpsePosition);
    }

    public void Heal(EntityUid target, FixedPoint2 amount)
    {
        var damage = _rmcDamageable.DistributeHealing(target, BruteGroup, amount);
        var totalHeal = damage.GetTotal();
        var leftover = amount - totalHeal;
        if (leftover > FixedPoint2.Zero)
            damage = _rmcDamageable.DistributeHealing(target, BurnGroup, leftover, damage);
        _damageable.TryChangeDamage(target, -damage, true);
    }

    private bool GetHiveCore(EntityUid xeno)
    {
        var cores = EntityQueryEnumerator<HiveCoreComponent, HiveMemberComponent>();
        while (cores.MoveNext(out var uid, out _, out _))
        {
            if (!_hive.FromSameHive(xeno, uid))
                continue;

            if (_mobState.IsDead(uid))
                continue;

            return true;
        }

        return false;
    }

    protected virtual void SacraficialHealShout(EntityUid xeno)
    {
    }

    protected virtual void SacraficialHealRespawn(EntityUid xeno, TimeSpan time, bool atCorpse = false, EntityCoordinates? corpse = null)
    {
    }

    public override void Update(float frameTime)
    {
        var time = _timing.CurTime;
        var healQuery = EntityQueryEnumerator<XenoBeingHealedComponent>();
        while (healQuery.MoveNext(out var uid, out var heal))
        {
            if (heal.HealStacks.Count == 0 || _mobState.IsDead(uid))
            {
                RemCompDeferred<XenoBeingHealedComponent>(uid);
                continue;
            }

            List<XenoHealStack> finishedStacks = new();

            foreach (var healStack in heal.HealStacks)
            {
                if (healStack.Charges <= 0)
                {
                    finishedStacks.Add(healStack);
                    continue;
                }

                if (healStack.NextHealAt > time)
                {
                    continue;
                }

                Dirty(uid, heal);

                Heal(uid, healStack.HealAmount);

                healStack.NextHealAt = time + healStack.TimeBetweenHeals;
                healStack.Charges = healStack.Charges - 1;

                if (!heal.ParallizeHealing)
                {
                    break;
                }
            }

            foreach (var stack in finishedStacks)
            {
                heal.HealStacks.Remove(stack);
            }
        }
    }
}
