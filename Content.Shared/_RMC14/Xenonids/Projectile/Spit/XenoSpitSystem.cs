using Content.Shared._RMC14.Armor;
using Content.Shared._RMC14.Atmos;
using Content.Shared._RMC14.Chemistry;
using Content.Shared._RMC14.Explosion;
using Content.Shared._RMC14.OnCollide;
using Content.Shared._RMC14.Shields;
using Content.Shared._RMC14.Slow;
using Content.Shared._RMC14.Xenonids.Hive;
using Content.Shared._RMC14.Xenonids.Projectile.Spit.Ball;
using Content.Shared._RMC14.Xenonids.Projectile.Spit.Charge;
using Content.Shared._RMC14.Xenonids.Projectile.Spit.Scattered;
using Content.Shared._RMC14.Xenonids.Projectile.Spit.Shield;
using Content.Shared._RMC14.Xenonids.Projectile.Spit.Slowing;
using Content.Shared._RMC14.Xenonids.Projectile.Spit.Stacks;
using Content.Shared._RMC14.Xenonids.Projectile.Spit.Standard;
using Content.Shared.ActionBlocker;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.Coordinates;
using Content.Shared.Damage;
using Content.Shared.DoAfter;
using Content.Shared.Effects;
using Content.Shared.Inventory;
using Content.Shared.Mobs.Systems;
using Content.Shared.Movement.Systems;
using Content.Shared.Popups;
using Content.Shared.Projectiles;
using Content.Shared.Stunnable;
using Content.Shared.Whitelist;
using Robust.Shared.Network;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Shared._RMC14.Xenonids.Projectile.Spit;

public sealed class XenoSpitSystem : EntitySystem
{
    [Dependency] private readonly ActionBlockerSystem _actionBlocker = default!;
    [Dependency] private readonly SharedAppearanceSystem _appearance = default!;
    [Dependency] private readonly SharedColorFlashEffectSystem _colorFlash = default!;
    [Dependency] private readonly DamageableSystem _damageable = default!;
    [Dependency] private readonly SharedDoAfterSystem _doAfter = default!;
    [Dependency] private readonly EntityWhitelistSystem _entityWhitelist = default!;
    [Dependency] private readonly SharedXenoHiveSystem _hive = default!;
    [Dependency] private readonly InventorySystem _inventory = default!;
    [Dependency] private readonly INetManager _net = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly MovementSpeedModifierSystem _movementSpeed = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly SharedStunSystem _stun = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly XenoProjectileSystem _xenoProjectile = default!;
    [Dependency] private readonly XenoShieldSystem _xenoShield = default!;
    [Dependency] private readonly SharedSolutionContainerSystem _solution = default!;
    [Dependency] private readonly IPrototypeManager _prototypeManager = default!;
    [Dependency] private readonly CMArmorSystem _armor = default!;
    [Dependency] private readonly RMCSlowSystem _slow = default!;

    private static readonly ProtoId<ReagentPrototype> AcidRemovedBy = "Water";

    private EntityQuery<ProjectileComponent> _projectileQuery;

    public override void Initialize()
    {
        _projectileQuery = GetEntityQuery<ProjectileComponent>();

        SubscribeLocalEvent<XenoSpitComponent, XenoSpitActionEvent>(OnXenoSpitAction);
        SubscribeLocalEvent<XenoSlowingSpitComponent, XenoSlowingSpitActionEvent>(OnXenoSlowingSpitAction);
        SubscribeLocalEvent<XenoScatteredSpitComponent, XenoScatteredSpitActionEvent>(OnXenoScatteredSpitAction);
        SubscribeLocalEvent<XenoChargeSpitComponent, XenoChargeSpitActionEvent>(OnXenoChargeSpitAction);

        SubscribeLocalEvent<XenoActiveChargingSpitComponent, ComponentStartup>(OnActiveChargingSpitAdded);
        SubscribeLocalEvent<XenoActiveChargingSpitComponent, ComponentRemove>(OnActiveChargingSpitRemove);
        SubscribeLocalEvent<XenoActiveChargingSpitComponent, CMGetArmorEvent>(OnActiveChargingSpitGetArmor);
        SubscribeLocalEvent<XenoActiveChargingSpitComponent, RefreshMovementSpeedModifiersEvent>(OnActiveChargingSpitRefreshSpeed);
        SubscribeLocalEvent<XenoActiveChargingSpitComponent, XenoGetSpitProjectileEvent>(OnActiveChargingSpitGetProjectile);

        SubscribeLocalEvent<XenoSlowingSpitProjectileComponent, ProjectileHitEvent>(OnXenoSlowingSpitHit, after: [typeof(CMClusterGrenadeSystem)]);

        SubscribeLocalEvent<XenoAcidBallComponent, XenoAcidBallActionEvent>(OnXenoAcidBallAction);
        SubscribeLocalEvent<XenoAcidBallComponent, XenoAcidBallDoAfterEvent>(OnXenoAcidBallDoAfter);

        SubscribeLocalEvent<ApplyAcidStacksComponent, ProjectileHitEvent>(OnApplyAcidStacksProjectileHit, after: [typeof(CMClusterGrenadeSystem)]);
        SubscribeLocalEvent<ApplyAcidStacksComponent, DamageCollideEvent>(OnApplyAcidStacksDamageCollide);

        SubscribeLocalEvent<XenoProjectileShieldOnHitComponent, ProjectileHitEvent>(OnShieldOnHit, after: [typeof(CMClusterGrenadeSystem)]);
        SubscribeLocalEvent<XenoProjectileShieldOnHitComponent, CMClusterSpawnedEvent>(OnShieldOnHitClusterSpawned);

        SubscribeLocalEvent<UserAcidedComponent, MapInitEvent>(OnUserAcidedMapInit);
        SubscribeLocalEvent<UserAcidedComponent, ComponentRemove>(OnUserAcidedRemove);
        SubscribeLocalEvent<UserAcidedComponent, ShowFireAlertEvent>(OnUserAcidedShowFireAlert);
        SubscribeLocalEvent<UserAcidedComponent, VaporHitEvent>(OnUserAcidedVaporHit);

        SubscribeLocalEvent<InventoryComponent, HitBySlowingSpitEvent>(_inventory.RelayEvent);

        SubscribeLocalEvent<DrainOnHitComponent, ProjectileHitEvent>(OnDrainOnHitProjectileHit, after: [typeof(CMClusterGrenadeSystem)]);
    }

    private void OnActiveChargingSpitRemove(Entity<XenoActiveChargingSpitComponent> ent, ref ComponentRemove args)
    {
        if (!TerminatingOrDeleted(ent))
        {
            _movementSpeed.RefreshMovementSpeedModifiers(ent);
            _armor.UpdateArmorValue((ent, null));
        }
    }

    private void OnActiveChargingSpitAdded(Entity<XenoActiveChargingSpitComponent> ent, ref ComponentStartup args)
    {
        _armor.UpdateArmorValue((ent, null));
    }

    private void OnActiveChargingSpitGetArmor(Entity<XenoActiveChargingSpitComponent> ent, ref CMGetArmorEvent args)
    {
        args.Armor += ent.Comp.Armor;
    }

    private void OnActiveChargingSpitRefreshSpeed(Entity<XenoActiveChargingSpitComponent> ent, ref RefreshMovementSpeedModifiersEvent args)
    {
        args.ModifySpeed(ent.Comp.Speed, ent.Comp.Speed);
    }

    private void OnActiveChargingSpitGetProjectile(Entity<XenoActiveChargingSpitComponent> ent, ref XenoGetSpitProjectileEvent args)
    {
        args.Id = ent.Comp.Projectile;
    }

    private void OnXenoSpitAction(Entity<XenoSpitComponent> xeno, ref XenoSpitActionEvent args)
    {
        if (args.Handled || args.Coords == null)
            return;

        var ev = new XenoGetSpitProjectileEvent(xeno.Comp.ProjectileId);
        RaiseLocalEvent(xeno, ref ev);

        args.Handled = _xenoProjectile.TryShoot(
            xeno,
            args.Coords.Value,
            xeno.Comp.PlasmaCost,
            ev.Id,
            xeno.Comp.Sound,
            1,
            Angle.Zero,
            xeno.Comp.Speed,
            target: args.Entity
        );

        if (RemCompDeferred<XenoActiveChargingSpitComponent>(xeno))
            _popup.PopupClient(Loc.GetString("cm-xeno-charge-spit-expire"), xeno, xeno, PopupType.SmallCaution);
    }

    private void OnXenoSlowingSpitAction(Entity<XenoSlowingSpitComponent> xeno, ref XenoSlowingSpitActionEvent args)
    {
        if (args.Handled || args.Coords == null)
            return;

        args.Handled = _xenoProjectile.TryShoot(
            xeno,
            args.Coords.Value,
            xeno.Comp.PlasmaCost,
            xeno.Comp.ProjectileId,
            xeno.Comp.Sound,
            1,
            Angle.Zero,
            xeno.Comp.Speed,
            target: args.Entity
        );
    }

    private void OnXenoScatteredSpitAction(Entity<XenoScatteredSpitComponent> xeno, ref XenoScatteredSpitActionEvent args)
    {
        if (args.Handled || args.Coords == null)
            return;

        args.Handled = _xenoProjectile.TryShoot(
            xeno,
            args.Coords.Value,
            xeno.Comp.PlasmaCost,
            xeno.Comp.ProjectileId,
            xeno.Comp.Sound,
            xeno.Comp.MaxProjectiles,
            xeno.Comp.MaxDeviation,
            xeno.Comp.Speed,
            target: args.Entity
        );
    }

    private void OnXenoChargeSpitAction(Entity<XenoChargeSpitComponent> xeno, ref XenoChargeSpitActionEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;

        var charging = EnsureComp<XenoActiveChargingSpitComponent>(xeno);
        charging.ExpiresAt = _timing.CurTime + xeno.Comp.Duration;
        charging.Armor = xeno.Comp.Armor;
        charging.Speed = xeno.Comp.Speed;
        Dirty(xeno, charging);

        _movementSpeed.RefreshMovementSpeedModifiers(xeno);

        _popup.PopupClient(Loc.GetString("cm-xeno-charge-spit"), xeno, xeno);
        if(_net.IsServer)
            SpawnAttachedTo(xeno.Comp.Effect, xeno.Owner.ToCoordinates());
    }

    private void OnXenoSlowingSpitHit(Entity<XenoSlowingSpitProjectileComponent> spit, ref ProjectileHitEvent args)
    {
        if (_net.IsClient)
            return;

        var target = args.Target;
        if (_hive.FromSameHive(spit.Owner, target))
        {
            QueueDel(spit);
            return;
        }

        if (spit.Comp.Slow > TimeSpan.Zero)
        {
            if (spit.Comp.SuperSlow)
                _slow.TrySuperSlowdown(target, spit.Comp.Slow);
            else
                _slow.TrySlowdown(target, spit.Comp.Slow);
        }

        var resisted = false;
        if (spit.Comp.ArmorResistsKnockdown)
        {
            var ev = new HitBySlowingSpitEvent(SlotFlags.OUTERCLOTHING | SlotFlags.INNERCLOTHING);
            RaiseLocalEvent(args.Target, ref ev);
            resisted = ev.Cancelled;
        }

        if (!resisted)
            _stun.TryParalyze(target, spit.Comp.Paralyze, true);

        _colorFlash.RaiseEffect(Color.Red, new List<EntityUid> { target }, Filter.Pvs(target));
    }

    private void OnXenoAcidBallAction(Entity<XenoAcidBallComponent> ent, ref XenoAcidBallActionEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;
        var ev = new XenoAcidBallDoAfterEvent(GetNetCoordinates(args.Target));
        var doAfter = new DoAfterArgs(EntityManager, ent, ent.Comp.Delay, ev, ent);
        _doAfter.TryStartDoAfter(doAfter);
    }

    private void OnXenoAcidBallDoAfter(Entity<XenoAcidBallComponent> ent, ref XenoAcidBallDoAfterEvent args)
    {
        if (args.Handled || args.Cancelled)
            return;

        var target = GetCoordinates(args.Coordinates);
        var targetMap = _transform.ToMapCoordinates(target);
        var origin = _transform.GetMapCoordinates(ent);
        if (origin.MapId != targetMap.MapId)
            return;

        var direction = targetMap.Position - origin.Position;
        var distance = Math.Min(ent.Comp.MaxRange, direction.Length());
        args.Handled = _xenoProjectile.TryShoot(
            ent,
            target,
            ent.Comp.PlasmaCost,
            ent.Comp.ProjectileId,
            null,
            1,
            Angle.Zero,
            ent.Comp.Speed,
            distance
        );

        if (args.Handled)
            _popup.PopupClient(Loc.GetString("rmc-xeno-acid-ball-shoot-self"), ent, ent);
    }

    private void OnApplyAcidStacksProjectileHit(Entity<ApplyAcidStacksComponent> ent, ref ProjectileHitEvent args)
    {
        if (args.Handled)
            return;

        ApplyAcidStacks(args.Target, ent.Comp.Amount, ent.Comp.Max, ent.Comp.Damage, ent.Comp.Whitelist);
    }

    private void OnApplyAcidStacksDamageCollide(Entity<ApplyAcidStacksComponent> ent, ref DamageCollideEvent args)
    {
        ApplyAcidStacks(args.Target, ent.Comp.Amount, ent.Comp.Max, ent.Comp.Damage, ent.Comp.Whitelist);
    }

    private void OnShieldOnHit(Entity<XenoProjectileShieldOnHitComponent> ent, ref ProjectileHitEvent args)
    {
        if (!_projectileQuery.TryComp(ent, out var projectile) ||
            projectile.Shooter is not { Valid: true } shooter)
        {
            return;
        }

        _xenoShield.ApplyShield(shooter, ent.Comp.Shield, ent.Comp.Amount, addShield: true, maxShield: ent.Comp.Max.Double());
    }

    private void OnShieldOnHitClusterSpawned(Entity<XenoProjectileShieldOnHitComponent> ent, ref CMClusterSpawnedEvent args)
    {
        var shooter = _projectileQuery.CompOrNull(ent)?.Shooter;
        foreach (var spawned in args.Spawned)
        {
            EnsureComp<XenoProjectileShieldOnHitComponent>(spawned);
            if (shooter != null)
            {
                var projectile = EnsureComp<ProjectileComponent>(spawned);
                projectile.Shooter = shooter;
                Dirty(spawned, projectile);
            }
        }
    }

    private void OnUserAcidedMapInit(Entity<UserAcidedComponent> ent, ref MapInitEvent args)
    {
        ent.Comp.ExpiresAt = _timing.CurTime + ent.Comp.Duration;
        Dirty(ent);
        UpdateAppearance(ent);
    }

    private void OnUserAcidedRemove(Entity<UserAcidedComponent> ent, ref ComponentRemove args)
    {
        _appearance.SetData(ent, UserAcidedVisuals.Acided, UserAcidedEffects.None);
    }

    private void OnUserAcidedShowFireAlert(Entity<UserAcidedComponent> ent, ref ShowFireAlertEvent args)
    {
        args.Show = true;
    }

    private void OnUserAcidedVaporHit(Entity<UserAcidedComponent> ent, ref VaporHitEvent args)
    {
        var solEnt = args.Solution;
        foreach (var (_, solution) in _solution.EnumerateSolutions((solEnt, solEnt)))
        {
            if (!solution.Comp.Solution.ContainsReagent(AcidRemovedBy, null))
                continue;

            RemCompDeferred<UserAcidedComponent>(ent);
            break;
        }
    }

    public void SetAcidCombo(Entity<UserAcidedComponent?> acided, TimeSpan duration, DamageSpecifier? damage, TimeSpan paralyze)
    {
        if (!Resolve(acided, ref acided.Comp, false))
            return;

        if (acided.Comp.Combo)
            return;

        acided.Comp.Combo = true;

        if (damage != null)
            acided.Comp.Damage = damage;

        if (duration != default)
        {
            var oldDuration = acided.Comp.Duration;
            acided.Comp.Duration = duration;
            acided.Comp.ExpiresAt = acided.Comp.ExpiresAt - oldDuration + duration;
        }

        if (paralyze != default)
            _stun.TryParalyze(acided.Owner, paralyze, true);

        Dirty(acided);
        UpdateAppearance((acided, acided.Comp));
    }

    private void UpdateAppearance(Entity<UserAcidedComponent> acided)
    {
        var effect = acided.Comp.Combo ? UserAcidedEffects.Enhanced : UserAcidedEffects.Normal;
        _appearance.SetData(acided, UserAcidedVisuals.Acided, effect);
    }

    public void Resist(Entity<UserAcidedComponent?> player)
    {
        if (!Resolve(player, ref player.Comp, false))
            return;

        if (!_actionBlocker.CanInteract(player, null))
            return;

        _popup.PopupEntity(Loc.GetString("rmc-acid-resist"), player, player);
        _stun.TryParalyze(player.Owner, player.Comp.ResistDuration, true);
        RemCompDeferred<UserAcidedComponent>(player);
    }

    private void ApplyAcidStacks(EntityUid target, int amount, int max, DamageSpecifier? damage, EntityWhitelist? whitelist)
    {
        if (!_entityWhitelist.IsWhitelistPassOrNull(whitelist, target))
            return;

        if (_mobState.IsDead(target))
            return;

        var victim = EnsureComp<VictimXenoAcidStacksComponent>(target);
        victim.Current = Math.Min(max, victim.Current + amount);
        victim.LastIncrement = _timing.CurTime;
        Dirty(target, victim);

        if (victim.Current >= max)
        {
            if (damage != null)
            {
                _damageable.TryChangeDamage(target, damage);
                _popup.PopupEntity(Loc.GetString("rmc-xeno-praetorian-acid-spit-hit-self"), target, target, PopupType.SmallCaution);
            }

            RemCompDeferred<VictimXenoAcidStacksComponent>(target);
        }
    }

    private void OnDrainOnHitProjectileHit(Entity<DrainOnHitComponent> spit, ref ProjectileHitEvent args)
    {
        if (_net.IsClient)
            return;

        var target = args.Target;
        if (_hive.FromSameHive(spit.Owner, target) || !_solution.TryGetSolution(target, spit.Comp.TargetSolution, out var solEnt, out var solu))
            return;

        if (solu == null || solEnt == null)
            return;

        //TODO RMC-14 resisting neuro should prevent medicine drain but not stim drain
        foreach (var chemical in solu.GetReagentPrototypes(_prototypeManager).Keys)
        {
            if (chemical.Group == spit.Comp.DrainGroup)
                _solution.RemoveReagent(solEnt.Value, chemical.ID, spit.Comp.DrainAmount);
        }
    }


    public override void Update(float frameTime)
    {
        var time = _timing.CurTime;

        var chargingQuery = EntityQueryEnumerator<XenoActiveChargingSpitComponent>();
        while (chargingQuery.MoveNext(out var uid, out var charging))
        {
            if (charging.ExpiresAt > time)
                continue;

            RemCompDeferred<XenoActiveChargingSpitComponent>(uid);

            if (!charging.DidPopup)
            {
                charging.DidPopup = true;
                _popup.PopupClient(Loc.GetString("cm-xeno-charge-spit-expire"), uid, uid, PopupType.SmallCaution);
            }
        }

        var acidedQuery = EntityQueryEnumerator<UserAcidedComponent>();
        while (acidedQuery.MoveNext(out var uid, out var acided))
        {
            if (time >= acided.ExpiresAt)
            {
                RemCompDeferred<UserAcidedComponent>(uid);
                continue;
            }

            if (time < acided.NextDamageAt)
                continue;

            acided.NextDamageAt = time + acided.DamageEvery;
            _damageable.TryChangeDamage(uid, acided.Damage, armorPiercing: acided.ArmorPiercing);
        }

        var stacks = EntityQueryEnumerator<VictimXenoAcidStacksComponent>();
        while (stacks.MoveNext(out var uid, out var victim))
        {
            if (time < victim.LastDecrement + victim.DecrementEvery ||
                time < victim.LastIncrement + victim.IncrementFor)
            {
                continue;
            }

            victim.Current--;
            victim.LastDecrement = _timing.CurTime;
            Dirty(uid, victim);

            if (victim.Current <= 0)
                RemCompDeferred<VictimXenoAcidStacksComponent>(uid);
        }
    }
}
