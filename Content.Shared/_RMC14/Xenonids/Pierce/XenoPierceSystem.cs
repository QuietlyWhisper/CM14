using Content.Shared._RMC14.Emote;
using Content.Shared._RMC14.Shields;
using Content.Shared._RMC14.Weapons.Melee;
using Content.Shared._RMC14.Xenonids.Plasma;
using Content.Shared.Coordinates;
using Content.Shared.Damage;
using Content.Shared.Effects;
using Content.Shared.FixedPoint;
using Content.Shared.Interaction;
using Content.Shared.Physics;
using Content.Shared.Weapons.Melee;
using Content.Shared.Weapons.Melee.Events;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Map;
using Robust.Shared.Network;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Player;
using System.Linq;
using System.Numerics;

namespace Content.Shared._RMC14.Xenonids.Pierce;

public sealed class XenoPierceSystem : EntitySystem
{
    [Dependency] private readonly XenoSystem _xeno = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly XenoPlasmaSystem _plasma = default!;
    [Dependency] private readonly SharedPhysicsSystem _physics = default!;
    [Dependency] private readonly SharedInteractionSystem _interaction = default!;
    [Dependency] private readonly SharedRMCEmoteSystem _emote = default!;
    [Dependency] private readonly DamageableSystem _damage = default!;
    [Dependency] private readonly SharedColorFlashEffectSystem _colorFlash = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly INetManager _net = default!;
    [Dependency] private readonly VanguardShieldSystem _vanguard = default!;
    [Dependency] private readonly SharedRMCMeleeWeaponSystem _rmcMelee = default!;

    private const int AttackMask = (int)(CollisionGroup.MobMask | CollisionGroup.Opaque);

    public override void Initialize()
    {
        SubscribeLocalEvent<XenoPierceComponent, XenoPierceActionEvent>(OnXenoPierceAction);
    }

    private void OnXenoPierceAction(Entity<XenoPierceComponent> xeno, ref XenoPierceActionEvent args)
    {
        //Note below is mostly all tail stab code
        var transform = Transform(xeno);

        var userCoords = _transform.GetMapCoordinates(xeno, transform);
        if (userCoords.MapId == MapId.Nullspace)
            return;

        var targetCoords = _transform.ToMapCoordinates(args.Target);
        if (userCoords.MapId != targetCoords.MapId)
            return;

        var range = xeno.Comp.Range.Float();
        var box = new Box2(userCoords.Position.X - 0.10f, userCoords.Position.Y, userCoords.Position.X + 0.10f, userCoords.Position.Y + range);

        var matrix = Vector2.Transform(targetCoords.Position, _transform.GetInvWorldMatrix(transform));
        var rotation = _transform.GetWorldRotation(xeno).RotateVec(-matrix).ToWorldAngle();
        var boxRotated = new Box2Rotated(box, rotation, userCoords.Position);

        // ray on the left side of the box
        var leftRay = new CollisionRay(boxRotated.BottomLeft, (boxRotated.TopLeft - boxRotated.BottomLeft).Normalized(), AttackMask);

        // ray on the right side of the box
        var rightRay = new CollisionRay(boxRotated.BottomRight, (boxRotated.TopRight - boxRotated.BottomRight).Normalized(), AttackMask);

        bool Ignore(EntityUid uid)
        {
            if (!_xeno.CanAbilityAttackTarget(xeno, uid))
                return true;

            return false;
        }

        var intersect = _physics.IntersectRayWithPredicate(transform.MapID, leftRay, range, Ignore, false);
        intersect = intersect.Concat(_physics.IntersectRayWithPredicate(transform.MapID, rightRay, range, Ignore, false));
        var results = intersect.Select(r => r.HitEntity).ToHashSet();

        var actualResults = new List<EntityUid>();
        foreach (var result in results)
        {
            if (!_interaction.InRangeUnobstructed(xeno.Owner, result, range: range))
                continue;

            actualResults.Add(result);
            if (xeno.Comp.MaxTargets != null && actualResults.Count >= xeno.Comp.MaxTargets)
                break;
        }

        _emote.TryEmoteWithChat(xeno, xeno.Comp.Emote, cooldown: xeno.Comp.EmoteCooldown);

        args.Handled = true;

        var filter = Filter.Pvs(transform.Coordinates, entityMan: EntityManager).RemoveWhereAttachedEntity(o => o == xeno.Owner);
        foreach (var hit in actualResults)
        {
            var attackedEv = new AttackedEvent(xeno, xeno, args.Target);
            RaiseLocalEvent(hit, attackedEv);

            var change = _damage.TryChangeDamage(hit, xeno.Comp.Damage, origin: xeno, armorPiercing: xeno.Comp.AP);

            if (change?.GetTotal() > FixedPoint2.Zero)
                _colorFlash.RaiseEffect(Color.Red, new List<EntityUid> { hit }, filter);

            if (_net.IsServer)
                SpawnAttachedTo(xeno.Comp.AttackEffect, hit.ToCoordinates());
        }

        if (actualResults.Count > 0)
            _rmcMelee.DoLunge(xeno, actualResults[0]);

        if (_net.IsServer)
            _audio.PlayPvs(xeno.Comp.Sound, xeno);

        if (actualResults.Count >= xeno.Comp.RechargeTargetsRequired)
            _vanguard.RegenShield(xeno);
    }
}
