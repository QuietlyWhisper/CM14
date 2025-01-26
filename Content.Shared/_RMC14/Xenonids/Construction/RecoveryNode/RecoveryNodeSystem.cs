using Content.Shared._RMC14.Xenonids.Heal;
using Content.Shared._RMC14.Xenonids.Hive;
using Content.Shared._RMC14.Xenonids.Rest;
using Content.Shared.Coordinates;
using Content.Shared.Damage;
using Content.Shared.Popups;
using Robust.Shared.Random;
using Robust.Shared.Timing;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Content.Shared._RMC14.Xenonids.Construction.RecoveryNode;

public sealed partial class RecoveryNodeSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _time = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly SharedXenoHiveSystem _hive = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly SharedXenoHealSystem _heal = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly DamageableSystem _damageable = default!;
    public override void Initialize()
    {
        base.Initialize();

    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var curTime = _time.CurTime;
        var recoverNodes = EntityQueryEnumerator<RecoveryNodeComponent>();

        while (recoverNodes.MoveNext(out var ent, out var comp))
        {
            if (comp.NextHealAt < curTime)
            {
                TryHealRandomXeno((ent, comp));
                comp.NextHealAt = null;
            }

            if (comp.NextHealAt is null)
            {
                comp.NextHealAt = curTime + comp.HealCooldown;
            }
        }
    }

    private bool TryHealRandomXeno(Entity<RecoveryNodeComponent> recoveryNode)
    {
        var (ent, comp) = recoveryNode;
        var nearbyEntities = _lookup.GetEntitiesInRange(ent, comp.HealRange);
        var possibleTargets = new List<EntityUid>();
        foreach (var nearbyEntity in nearbyEntities)
        {
            if (!_hive.FromSameHive(ent, nearbyEntity) || !HasComp<XenoComponent>(nearbyEntity) || !HasComp<XenoRestingComponent>(nearbyEntity) ||
                !TryComp<DamageableComponent>(nearbyEntity, out var damageComp) || damageComp.TotalDamage <= 0)
            {
                continue;
            }

            possibleTargets.Add(nearbyEntity);
        }

        if (possibleTargets.Count == 0)
        {
            return false;
        }
        var selectedTarget = _random.Pick(possibleTargets);

        _heal.Heal(selectedTarget, comp.HealAmount);
        SpawnAttachedTo(comp.HealEffect, selectedTarget.ToCoordinates());
        _popup.PopupClient(Loc.GetString("rmc-xeno-construction-recovery-node-heal-target"), selectedTarget, selectedTarget);
        return true;
    }
}
