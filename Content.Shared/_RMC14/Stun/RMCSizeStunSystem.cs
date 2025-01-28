using System.Numerics;
using Content.Shared._RMC14.Pulling;
using Content.Shared._RMC14.Slow;
using Content.Shared.ActionBlocker;
using Content.Shared.Damage.Systems;
using Content.Shared.Movement.Systems;
using Content.Shared.Popups;
using Content.Shared.Projectiles;
using Content.Shared.Standing;
using Content.Shared.Stunnable;
using Content.Shared.Throwing;
using Robust.Shared.Network;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Timing;

namespace Content.Shared._RMC14.Stun;

public sealed class RMCSizeStunSystem : EntitySystem
{
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly StandingStateSystem _stand = default!;
    [Dependency] private readonly StaminaSystem _stamina = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly ActionBlockerSystem _blocker = default!;
    [Dependency] private readonly ThrowingSystem _throwing = default!;
    [Dependency] private readonly MovementSpeedModifierSystem _movementSpeed = default!;
    [Dependency] private readonly INetManager _net = default!;
    [Dependency] private readonly SharedStunSystem _stun = default!;
    [Dependency] private readonly SharedPhysicsSystem _physics = default!;
    [Dependency] private readonly RMCPullingSystem _rmcPulling = default!;
    [Dependency] private readonly RMCSlowSystem _slow = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<RMCStunOnHitComponent, MapInitEvent>(OnSizeStunMapInit);
        SubscribeLocalEvent<RMCStunOnHitComponent, ProjectileHitEvent>(OnHit);
    }

    public bool IsHumanoidSized(Entity<RMCSizeComponent> ent)
    {
        return ent.Comp.Size <= RMCSizes.Humanoid;
    }

    public bool IsXenoSized(Entity<RMCSizeComponent> ent)
    {
        return ent.Comp.Size >= RMCSizes.VerySmallXeno;
    }

    public bool TryGetSize(EntityUid ent, out RMCSizes size)
    {
        size = default;
        if (!TryComp(ent, out RMCSizeComponent? sizeComp))
            return false;

        size = sizeComp.Size;
        return true;
    }

    private void OnSizeStunMapInit(Entity<RMCStunOnHitComponent> projectile, ref MapInitEvent args)
    {
        projectile.Comp.ShotFrom = _transform.GetMoverCoordinates(projectile.Owner);
        Dirty(projectile);
    }

    private void OnHit(Entity<RMCStunOnHitComponent> bullet, ref ProjectileHitEvent args)
    {
        if (_net.IsClient || bullet.Comp.ShotFrom == null)
            return;

        var distance = (_transform.GetMoverCoordinates(args.Target).Position - bullet.Comp.ShotFrom.Value.Position).Length();

        if (distance > bullet.Comp.MaxRange || _stand.IsDown(args.Target))
            return;

        if (!TryComp<RMCSizeComponent>(args.Target, out var size) || size.Size >= RMCSizes.Big)
            return;

        //TODO Camera Shake

        //Knockback
        if (_blocker.CanMove(args.Target))
        {

            _physics.SetLinearVelocity(args.Target, Vector2.Zero);
            _physics.SetAngularVelocity(args.Target, 0f);

            var vec = _transform.GetMoverCoordinates(args.Target).Position - bullet.Comp.ShotFrom.Value.Position;
            if (vec.Length() != 0)
            {
                _rmcPulling.TryStopPullsOn(args.Target);
                var direction = vec.Normalized();
                _throwing.TryThrow(args.Target, direction, 1, animated: false, playSound: false, doSpin: false);
                // RMC-14 TODO Thrown into obstacle mechanics
            }
        }

        //Stun part
        if (IsXenoSized((args.Target, size)))
        {
            var stun = bullet.Comp.StunTime;
            var superSlow = bullet.Comp.SuperSlowTime;
            var slow = bullet.Comp.SlowTime;

            if (bullet.Comp.LosesEffectWithRange)
            {
                stun -= TimeSpan.FromSeconds(distance / 50);
                superSlow -= TimeSpan.FromSeconds(distance / 10);
                slow -= TimeSpan.FromSeconds(distance / 5);
            }

            _stun.TryParalyze(args.Target, stun, true);
            _slow.TrySlowdown(args.Target, slow);
            _slow.TrySuperSlowdown(args.Target, superSlow);

            _popup.PopupEntity(Loc.GetString("rmc-xeno-stun-shaken"), args.Target, args.Target, PopupType.MediumCaution);
        }
        else
            _stamina.TakeStaminaDamage(args.Target, args.Damage.GetTotal().Float());
    }
}
