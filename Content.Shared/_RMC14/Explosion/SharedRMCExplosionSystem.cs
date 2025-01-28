using Content.Shared._RMC14.Atmos;
using Content.Shared._RMC14.Slow;
using Content.Shared._RMC14.Stun;
using Content.Shared.Body.Systems;
using Content.Shared.Coordinates;
using Content.Shared.Damage.Prototypes;
using Content.Shared.Explosion;
using Content.Shared.FixedPoint;
using Content.Shared.Standing;
using Content.Shared.Stunnable;
using Content.Shared.Throwing;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Shared._RMC14.Explosion;

public abstract class SharedRMCExplosionSystem : EntitySystem
{
    [Dependency] private readonly SharedBodySystem _body = default!;
    [Dependency] private readonly EntityLookupSystem _entityLookup = default!;
    [Dependency] private readonly IMapManager _map = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly SharedRMCFlammableSystem _rmcFlammable = default!;
    [Dependency] private readonly RMCSizeStunSystem _sizeStun = default!;
    [Dependency] private readonly StandingStateSystem _standing = default!;
    [Dependency] private readonly SharedStunSystem _stun = default!;
    [Dependency] private readonly ThrowingSystem _throwing = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly RMCSlowSystem _slow = default!;

    private static readonly ProtoId<DamageTypePrototype> StructuralDamage = "Structural";

    private readonly HashSet<Entity<RMCWallExplosionDeletableComponent>> _walls = new();

    public override void Initialize()
    {
        SubscribeLocalEvent<CMExplosionEffectComponent, CMExplosiveTriggeredEvent>(OnExplosionEffectTriggered);

        SubscribeLocalEvent<RMCExplosiveDeleteWallsComponent, CMExplosiveTriggeredEvent>(OnDeleteWallsTriggered);

        SubscribeLocalEvent<ExplosionRandomResistanceComponent, GetExplosionResistanceEvent>(OnExplosionRandomResistanceGet);

        SubscribeLocalEvent<StunOnExplosionReceivedComponent, ExplosionReceivedEvent>(OnStunOnExplosionReceivedBeforeExplode);

        SubscribeLocalEvent<DestroyedByExplosionTypeComponent, ExplosionReceivedEvent>(OnDestroyedByExplosionReceived);

        SubscribeLocalEvent<MobGibbedByExplosionTypeComponent, ExplosionReceivedEvent>(OnMobGibbedByExplosionReceived);
    }

    private void OnExplosionEffectTriggered(Entity<CMExplosionEffectComponent> ent, ref CMExplosiveTriggeredEvent args)
    {
        DoEffect(ent);
    }

    private void OnDeleteWallsTriggered(Entity<RMCExplosiveDeleteWallsComponent> ent, ref CMExplosiveTriggeredEvent args)
    {
        _walls.Clear();
        _entityLookup.GetEntitiesInRange(ent.Owner.ToCoordinates(), ent.Comp.Range, _walls);

        foreach (var wall in _walls)
        {
            QueueDel(wall);
        }
    }

    private void OnExplosionRandomResistanceGet(Entity<ExplosionRandomResistanceComponent> ent, ref GetExplosionResistanceEvent args)
    {
        // this only gets used on the server so randomness isn't an issue for prediction
        var resistance = _random.NextFloat(ent.Comp.Min.Float(), ent.Comp.Max.Float());
        args.DamageCoefficient *= resistance;
    }

    public void ChangeExplosionStunResistance(EntityUid ent, StunOnExplosionReceivedComponent? comp, bool isStunnable)
    {
        if (!Resolve(ent, ref comp, false))
            return;

        comp.Weak = isStunnable;
    }

    private void OnStunOnExplosionReceivedBeforeExplode(Entity<StunOnExplosionReceivedComponent> ent, ref ExplosionReceivedEvent args)
    {
        var damage = args.Damage.GetTotal();
        var factor = Math.Round(damage.Double() * 0.05) / 2;
        factor = Math.Min(20, factor);

        // TODO RMC14 don't reduce if explosion is on same tile
        if (_standing.IsDown(ent))
            factor *= 0.5;

        _sizeStun.TryGetSize(ent, out var size);
        if (factor > 0 && ent.Comp.Weak)
        {
            var stunTime = TimeSpan.FromSeconds(factor / 2.5);
            _stun.TryStun(ent, stunTime, true);
            _stun.TryKnockdown(ent, stunTime, true);

            if (size < RMCSizes.Big)
            {
                _slow.TrySlowdown(ent, TimeSpan.FromSeconds(factor));
                _slow.TrySuperSlowdown(ent, TimeSpan.FromSeconds(factor / 2));
            }
            else
                _slow.TrySlowdown(ent, TimeSpan.FromSeconds(factor / 3));

            var pos = _transform.GetWorldPosition(ent);
            var dir = pos - args.Epicenter.Position;
            if (dir.IsLengthZero())
                dir = _random.NextVector2();

            dir = dir.Normalized(); // TODO RMC14 size-based throw ranges and speeds
            _throwing.TryThrow(ent, dir, 5);
        }
        else if (factor > 10)
        {
            factor /= 5;
            var stunTime = TimeSpan.FromSeconds(factor / 5);
            _stun.TryStun(ent, stunTime, true);
            _stun.TryKnockdown(ent, stunTime, true);
            if (size < RMCSizes.Big)
            {
                _slow.TrySlowdown(ent, TimeSpan.FromSeconds(factor));
                _slow.TrySuperSlowdown(ent, TimeSpan.FromSeconds(factor / 2));
            }
            else
                _slow.TrySlowdown(ent, TimeSpan.FromSeconds(factor / 3));
        }
    }

    private void OnDestroyedByExplosionReceived(Entity<DestroyedByExplosionTypeComponent> ent, ref ExplosionReceivedEvent args)
    {
        if (args.Explosion != ent.Comp.Explosion ||
            args.Damage.GetTotal() < ent.Comp.Threshold)
        {
            return;
        }

        if (!TerminatingOrDeleted(ent))
            QueueDel(ent);
    }

    private void OnMobGibbedByExplosionReceived(Entity<MobGibbedByExplosionTypeComponent> ent, ref ExplosionReceivedEvent args)
    {
        if (args.Explosion != ent.Comp.Explosion)
            return;

        var total = FixedPoint2.Zero;
        foreach (var (type, amount) in args.Damage.DamageDict)
        {
            if (type == StructuralDamage)
                continue;

            total += amount;
        }

        if (total < ent.Comp.Threshold)
            return;

        if (!TerminatingOrDeleted(ent))
            _body.GibBody(ent);
    }

    public void DoEffect(Entity<CMExplosionEffectComponent> ent)
    {
        if (ent.Comp.ShockWave is { } shockwave)
            SpawnNextToOrDrop(shockwave, ent);

        if (ent.Comp.Explosion is { } explosion)
            SpawnNextToOrDrop(explosion, ent);

        if (ent.Comp.MaxShrapnel > 0)
        {
            foreach (var effect in ent.Comp.ShrapnelEffects)
            {
                var shrapnelCount = _random.Next(ent.Comp.MinShrapnel, ent.Comp.MaxShrapnel);
                for (var i = 0; i < shrapnelCount; i++)
                {
                    var angle = _random.NextAngle();
                    var direction = angle.ToVec().Normalized() * 10;
                    var shrapnel = SpawnNextToOrDrop(effect, ent);
                    _throwing.TryThrow(shrapnel, direction, ent.Comp.ShrapnelSpeed / 10);
                }
            }
        }
    }

    public void TryDoEffect(Entity<CMExplosionEffectComponent?> ent)
    {
        if (!Resolve(ent, ref ent.Comp, false))
            return;

        DoEffect((ent, ent.Comp));
    }

    public virtual void QueueExplosion(
        MapCoordinates epicenter,
        string typeId,
        float totalIntensity,
        float slope,
        float maxTileIntensity,
        EntityUid? cause,
        float tileBreakScale = 1f,
        int maxTileBreak = int.MaxValue,
        bool canCreateVacuum = true,
        bool addLog = true)
    {
    }

    public virtual void TriggerExplosive(
        EntityUid uid,
        bool delete = true,
        float? totalIntensity = null,
        float? radius = null,
        EntityUid? user = null)
    {
    }
}
