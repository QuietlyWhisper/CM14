﻿using System.Numerics;
using Content.Shared._RMC14.Marines.Skills;
using Content.Shared._RMC14.Weapons.Ranged.Whitelist;
using Content.Shared.Hands;
using Content.Shared.Hands.Components;
using Content.Shared.Physics;
using Content.Shared.Popups;
using Content.Shared.Projectiles;
using Content.Shared.Timing;
using Content.Shared.Weapons.Ranged.Components;
using Content.Shared.Weapons.Ranged.Events;
using Content.Shared.Weapons.Ranged.Systems;
using Content.Shared.Whitelist;
using Content.Shared.Wieldable;
using Content.Shared.Wieldable.Components;
using Robust.Shared.Containers;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Events;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Timing;

namespace Content.Shared._RMC14.Weapons.Ranged;

public sealed class CMGunSystem : EntitySystem
{
    [Dependency] private readonly SharedBroadphaseSystem _broadphase = default!;
    [Dependency] private readonly SharedContainerSystem _container = default!;
    [Dependency] private readonly SharedGunSystem _gun = default!;
    [Dependency] private readonly SharedPhysicsSystem _physics = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly SkillsSystem _skills = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly UseDelaySystem _useDelay = default!;
    [Dependency] private readonly EntityWhitelistSystem _whitelist = default!;
        [Dependency] private readonly FixtureSystem _fixtures = default!;

    private EntityQuery<PhysicsComponent> _physicsQuery;
    private EntityQuery<ProjectileComponent> _projectileQuery;

    public override void Initialize()
    {
        _physicsQuery = GetEntityQuery<PhysicsComponent>();
        _projectileQuery = GetEntityQuery<ProjectileComponent>();

        SubscribeLocalEvent<ShootAtFixedPointComponent, AmmoShotEvent>(OnShootAtFixedPointShot);

        SubscribeLocalEvent<ProjectileFixedDistanceComponent, PreventCollideEvent>(OnCollisionCheckArc);
        SubscribeLocalEvent<ProjectileFixedDistanceComponent, ComponentRemove>(OnEventToStopProjectile);
        SubscribeLocalEvent<ProjectileFixedDistanceComponent, PhysicsSleepEvent>(OnEventToStopProjectile);

        SubscribeLocalEvent<GunShowUseDelayComponent, GunShotEvent>(OnShowUseDelayShot);
        SubscribeLocalEvent<GunShowUseDelayComponent, ItemWieldedEvent>(OnShowUseDelayWielded);

        SubscribeLocalEvent<GunUserWhitelistComponent, AttemptShootEvent>(OnGunUserWhitelistAttemptShoot);

        SubscribeLocalEvent<GunUnskilledPenaltyComponent, GotEquippedHandEvent>(TryRefreshGunModifiers);
        SubscribeLocalEvent<GunUnskilledPenaltyComponent, GotUnequippedHandEvent>(TryRefreshGunModifiers);
        SubscribeLocalEvent<GunUnskilledPenaltyComponent, GunRefreshModifiersEvent>(OnGunUnskilledPenaltyRefresh);

        SubscribeLocalEvent<GunDamageModifierComponent, AmmoShotEvent>(OnGunDamageModifierAmmoShot);
        SubscribeLocalEvent<GunDamageModifierComponent, MapInitEvent>(OnGunDamageModifierMapInit);

        SubscribeLocalEvent<GunSkilledRecoilComponent, GotEquippedHandEvent>(TryRefreshGunModifiers);
        SubscribeLocalEvent<GunSkilledRecoilComponent, GotUnequippedHandEvent>(TryRefreshGunModifiers);
        SubscribeLocalEvent<GunSkilledRecoilComponent, ItemWieldedEvent>(TryRefreshGunModifiers);
        SubscribeLocalEvent<GunSkilledRecoilComponent, ItemUnwieldedEvent>(TryRefreshGunModifiers);
        SubscribeLocalEvent<GunSkilledRecoilComponent, GunRefreshModifiersEvent>(OnRecoilSkilledRefreshModifiers);
    }

    /// <summary>
    /// Shoot at a targeted point's coordinates. The projectile will stop at that location instead of continuing on until it hits something.
    /// There is also an option to arc the projectile with ShootArc or ProjectileArcing = true, making it ignore most collision.
    /// </summary>
    /// <remarks>
    /// For some reason, the engine seem to cause MaxFixedRange's conversion to actual projectile max ranges of around +1 tile.
    /// As a result, conversions should be 1 less than max_range when porting, and the minimum range for this feature is around 2 tiles.
    /// This could be manually tweaked try and fix it, but the math seems like it should be fine and it's predictable enough to be worked around for now.
    /// </remarks>
    private void OnShootAtFixedPointShot(Entity<ShootAtFixedPointComponent> ent, ref AmmoShotEvent args)
    {
        if (!TryComp(ent, out GunComponent? gun) ||
            gun.ShootCoordinates is not { } target)
        {
            return;
        }

        // Find start and end coordinates for vector.
        var from = _transform.GetMapCoordinates(ent);
        var to = _transform.ToMapCoordinates(target);
        // Must be same map.
        if (from.MapId != to.MapId)
            return;

        // Calculate vector, cancel if it ends up at 0.
        var direction = to.Position - from.Position;
        if (direction == Vector2.Zero)
            return;

        var distance = ent.Comp.MaxRange != null ? Math.Min(ent.Comp.MaxRange.Value, direction.Length()) : direction.Length();
        var time = _timing.CurTime;
        var normalized = direction.Normalized();

        // Send each FiredProjectile with a PhysicsComponent off with the same Vector. Max 
        foreach (var projectile in args.FiredProjectiles)
        {
            if (!_physicsQuery.TryComp(projectile, out var physics))
                continue;

            // Calculate needed impulse to get to target, remove all velocity from projectile, then apply.
            var impulse = normalized * gun.ProjectileSpeedModified * physics.Mass;
            _physics.SetLinearVelocity(projectile, Vector2.Zero, body: physics);
            _physics.ApplyLinearImpulse(projectile, impulse, body: physics);
            _physics.SetBodyStatus(projectile, physics, BodyStatus.InAir);

            // Apply the ProjectileFixedDistanceComponent onto each fired projectile, which both holds the FlyEndTime to be continually checked
            // and will trigger the OnEventToStopProjectile function once the PFD Component is deleted at that time. See Update()
            var comp = EnsureComp<ProjectileFixedDistanceComponent>(projectile);

            // Transfer arcing to the projectile.
            if (Comp<ShootAtFixedPointComponent>(ent).ShootArc)
                comp.ProjectileArcing = true;

            float ammoDistance = (float)Comp<ShootAtFixedPointComponent>(ent).MaxFixedRange;

            // Take the lowest nonzero MaxFixedRange between projectile and gun for the capped vector length.
            if (TryComp(projectile, out ProjectileComponent? normalProjectile) && normalProjectile.MaxFixedRange > 0)
            {
                if (ammoDistance > 0)
                    ammoDistance = float.Min(ammoDistance, (float)normalProjectile.MaxFixedRange);
                else
                    ammoDistance = (float)normalProjectile.MaxFixedRange;
            }
            // Calculate travel time and equivalent distance based either on click location or calculated max range, whichever is shorter.
            if (ammoDistance > 0)
            {
                float cappedRange = float.Min(direction.Length(), ammoDistance);
                comp.FlyEndTime = time + TimeSpan.FromSeconds(cappedRange / gun.ProjectileSpeedModified);
            }
            else
                comp.FlyEndTime = time + TimeSpan.FromSeconds(distance / gun.ProjectileSpeedModified);
        }
    }

    /// <summary>
    /// If the projectile collides with anything that doesn't have CollisionGroup.Impassable like walls, and it's arcing, ignore the collision.
    /// </summary>
    private void OnCollisionCheckArc(Entity<ProjectileFixedDistanceComponent> ent, ref PreventCollideEvent args)
    {
        int otherLayers = (int)args.OtherFixture.CollisionLayer;
        int impassableLayer = (int)CollisionGroup.Impassable;
        if (((Comp<ProjectileFixedDistanceComponent>(ent).ProjectileArcing) && !((args.OtherFixture.CollisionLayer & impassableLayer) == impassableLayer)))
            args.Cancelled = true;
        return;
    }

    private void OnEventToStopProjectile<T>(Entity<ProjectileFixedDistanceComponent> ent, ref T args)
    {
        StopProjectile(ent);
    }

    private void OnShowUseDelayShot(Entity<GunShowUseDelayComponent> ent, ref GunShotEvent args)
    {
        UpdateDelay(ent);
    }

    private void OnShowUseDelayWielded(Entity<GunShowUseDelayComponent> ent, ref ItemWieldedEvent args)
    {
        UpdateDelay(ent);
    }

    private void OnGunUserWhitelistAttemptShoot(Entity<GunUserWhitelistComponent> ent, ref AttemptShootEvent args)
    {
        if (args.Cancelled)
            return;

        if (_whitelist.IsValid(ent.Comp.Whitelist, args.User))
            return;

        args.Cancelled = true;
        _popup.PopupClient(Loc.GetString("cm-gun-unskilled", ("gun", ent.Owner)), args.User, args.User);
    }

    private void OnGunUnskilledPenaltyRefresh(Entity<GunUnskilledPenaltyComponent> ent, ref GunRefreshModifiersEvent args)
    {
        if (TryGetUserSkills(ent, out var skills) &&
            skills.Comp.Skills.Firearms >= ent.Comp.Firearms)
        {
            return;
        }

        args.MinAngle += ent.Comp.AngleIncrease;
        args.MaxAngle += ent.Comp.AngleIncrease;
    }

    private void OnGunDamageModifierMapInit(Entity<GunDamageModifierComponent> ent, ref MapInitEvent args)
    {
        RefreshGunDamageMultiplier((ent.Owner, ent.Comp));
    }

    private void OnGunDamageModifierAmmoShot(Entity<GunDamageModifierComponent> ent, ref AmmoShotEvent args)
    {
        foreach (var projectile in args.FiredProjectiles)
        {
            if (!_projectileQuery.TryGetComponent(projectile, out var comp))
                continue;

            comp.Damage *= ent.Comp.ModifiedMultiplier;
        }
    }

    private void OnRecoilSkilledRefreshModifiers(Entity<GunSkilledRecoilComponent> ent, ref GunRefreshModifiersEvent args)
    {
        if (!TryGetUserSkills(ent, out var user) ||
            !_skills.HasSkills((user, user), in ent.Comp.Skills))
        {
            return;
        }

        if (ent.Comp.MustBeWielded && CompOrNull<WieldableComponent>(ent)?.Wielded != true)
            return;

        args.CameraRecoilScalar = 0;
    }

    private void StopProjectile(Entity<ProjectileFixedDistanceComponent> projectile)
    {
        if (!_physicsQuery.TryGetComponent(projectile, out var physics))
            return;

        _physics.SetLinearVelocity(projectile, Vector2.Zero, body: physics);
        _physics.SetBodyStatus(projectile, physics, BodyStatus.OnGround);

        if (physics.Awake)
            _broadphase.RegenerateContacts(projectile, physics);
    }

    private void UpdateDelay(Entity<GunShowUseDelayComponent> ent)
    {
        if (!TryComp(ent, out GunComponent? gun))
            return;

        var remaining = gun.NextFire - _timing.CurTime;
        if (remaining <= TimeSpan.Zero)
            return;

        var useDelay = EnsureComp<UseDelayComponent>(ent);
        _useDelay.SetLength((ent, useDelay), remaining, ent.Comp.DelayId);
        _useDelay.TryResetDelay((ent, useDelay), false, ent.Comp.DelayId);
    }

    private void TryRefreshGunModifiers<TComp, TEvent>(Entity<TComp> ent, ref TEvent args) where TComp : IComponent?
    {
        if (TryComp(ent, out GunComponent? gun))
            _gun.RefreshModifiers((ent, gun));
    }

    private bool TryGetUserSkills(EntityUid gun, out Entity<SkillsComponent> user)
    {
        user = default;
        if (!_container.TryGetContainingContainer((gun, null), out var container) ||
            !HasComp<HandsComponent>(container.Owner) ||
            !TryComp(container.Owner, out SkillsComponent? skills))
        {
            return false;
        }

        user = (container.Owner, skills);
        return true;
    }

    public void RefreshGunDamageMultiplier(Entity<GunDamageModifierComponent?> gun)
    {
        gun.Comp = EnsureComp<GunDamageModifierComponent>(gun);

        var ev = new GetGunDamageModifierEvent(gun.Comp.Multiplier);
        RaiseLocalEvent(gun, ref ev);

        gun.Comp.ModifiedMultiplier = ev.Multiplier;
    }

    public override void Update(float frameTime)
    {
        var time = _timing.CurTime;
        var query = EntityQueryEnumerator<ProjectileFixedDistanceComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            if (time < comp.FlyEndTime)
                continue;

            RemCompDeferred<ProjectileFixedDistanceComponent>(uid);
        }
    }
}
