using Content.Shared._RMC14.Slow;
using Content.Shared.Movement.Systems;
using Content.Shared.Standing;
using Content.Shared.Weapons.Melee.Events;

namespace Content.Shared._RMC14.Xenonids.MeleeSlow;

public sealed class XenoMeleeSlowSystem : EntitySystem
{
    [Dependency] private readonly MovementSpeedModifierSystem _movementSpeed = default!;
    [Dependency] private readonly StandingStateSystem _standing = default!;
    [Dependency] private readonly XenoSystem _xeno = default!;
    [Dependency] private readonly RMCSlowSystem _slow = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<XenoMeleeSlowComponent, MeleeHitEvent>(OnHit);
    }

    private void OnHit(Entity<XenoMeleeSlowComponent> xeno, ref MeleeHitEvent args)
    {
        if (!args.IsHit || args.HitEntities.Count == 0)
            return;

        foreach (var entity in args.HitEntities)
        {
            if (!_xeno.CanAbilityAttackTarget(xeno, entity))
                return;

            if (xeno.Comp.RequiresKnockDown && !_standing.IsDown(entity))
                return;

            _slow.TrySlowdown(entity, xeno.Comp.SlowTime, ignoreDurationModifier: true);

            break;
        }
    }
}
