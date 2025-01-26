using Content.Server._RMC14.Damage;
using Content.Server.Ghost.Roles;
using Content.Server.Ghost.Roles.Events;
using Content.Shared._RMC14.Dropship;
using Content.Shared._RMC14.Xenonids;
using Content.Shared._RMC14.Xenonids.Construction;
using Content.Shared._RMC14.Xenonids.Egg;
using Content.Shared._RMC14.Xenonids.Evolution;
using Content.Shared._RMC14.Xenonids.Hive;
using Content.Shared.Damage;
using Content.Shared.Destructible;
using Content.Shared.FixedPoint;
using Content.Shared.Ghost.Roles.Components;
using Content.Shared.Mobs.Systems;
using Robust.Shared.Network;
using Robust.Shared.Timing;
using Robust.Shared.Utility;

namespace Content.Server._RMC14.Xenonids.Construction;

public sealed class XenoPylonSystem : SharedXenoPylonSystem
{
    [Dependency] private readonly DamageableSystem _damageable = default!;
    [Dependency] private readonly XenoEvolutionSystem _evolution = default!;
    [Dependency] private readonly GhostRoleSystem _ghostRole = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly INetManager _net = default!;
    [Dependency] private readonly RMCDamageableSystem _rmcDamageable = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly SharedXenoHiveSystem _hive = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<DropshipHijackStartEvent>(OnDropshipHijackStart);

        SubscribeLocalEvent<HiveCoreComponent, DestructionEventArgs>(OnHiveCoreDestruction);

        SubscribeLocalEvent<XenoComponent, GhostRoleSpawnerUsedEvent>(OnXenoSpawnerUsed);

        SubscribeLocalEvent<XenoHiveCoreRoleComponent, MapInitEvent>(OnCoreRoleMapInit);
        SubscribeLocalEvent<XenoHiveCoreRoleComponent, GhostRoleSpawnerUsedEvent>(OnCoreRoleSpawnerUsed);
    }

    private void OnDropshipHijackStart(ref DropshipHijackStartEvent ev)
    {
        var cores = EntityQueryEnumerator<HiveCoreComponent, TransformComponent>();
        while (cores.MoveNext(out var uid, out _, out var xform))
        {
            if (xform.ParentUid != ev.Dropship)
                QueueDel(uid);
        }
    }

    private void OnHiveCoreDestruction(Entity<HiveCoreComponent> ent, ref DestructionEventArgs args)
    {
        if (_hive.GetHive(ent.Owner) is {} hive)
            hive.Comp.NewCoreAt = _timing.CurTime + hive.Comp.NewCoreCooldown;
    }

    private void OnXenoSpawnerUsed(Entity<XenoComponent> xeno, ref GhostRoleSpawnerUsedEvent args)
    {
        _hive.SetSameHive(args.Spawner, xeno.Owner);

        if (TryComp(args.Spawner, out HivePylonComponent? core))
            core.LiveLesserDrones.Add(xeno);
    }

    private void OnCoreRoleMapInit(Entity<XenoHiveCoreRoleComponent> ent, ref MapInitEvent args)
    {
        _ghostRole.UpdateAllEui();
    }

    private void OnCoreRoleSpawnerUsed(Entity<XenoHiveCoreRoleComponent> ent, ref GhostRoleSpawnerUsedEvent args)
    {
        ent.Comp.Core = args.Spawner;
    }

    private void UpdateGhostRoles(Entity<HivePylonComponent, GhostRoleMobSpawnerComponent> coreEnt)
    {
        var (uid, core, spawner) = coreEnt;
        for (var i = core.LiveLesserDrones.Count - 1; i >= 0; i--)
        {
            var drone = core.LiveLesserDrones[i];
            if (TerminatingOrDeleted(drone) ||
                !HasComp<XenoComponent>(drone) ||
                _mobState.IsDead(drone))
            {
                core.LiveLesserDrones.RemoveSwap(i);
                core.CurrentLesserDrones = Math.Max(0, core.CurrentLesserDrones - 1);
            }
        }

        _ghostRole.SetCurrent((uid, spawner), core.LiveLesserDrones.Count);

        if (!_evolution.HasLiving<XenoComponent>(1) &&
            !_evolution.HasLiving<XenoEvolutionGranterComponent>(1))
        {
            _ghostRole.SetAvailable((uid, spawner), 0);
            return;
        }

        var living = _evolution.GetLiving<XenoComponent>(x => x.Comp.CountedInSlots);
        var available = Math.Max(core.MinimumLesserDrones, living / core.XenosPerLesserDrone);
        core.MaxLesserDrones = available;

        var time = _timing.CurTime;
        if (time > core.NextLesserDroneAt)
        {
            var hasOvipositor = _evolution.HasLiving<XenoAttachedOvipositorComponent>(1);
            core.NextLesserDroneAt = time + (hasOvipositor ? core.NextLesserDroneOviCooldown : core.NextLesserDroneCooldown * 2);
            core.CurrentLesserDrones = Math.Min(core.MaxLesserDrones, core.CurrentLesserDrones + 1);
        }

        _ghostRole.SetAvailable((uid, spawner), core.CurrentLesserDrones);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (_net.IsClient)
            return;

        // TODO RMC14 lesser drone job bans
        // TODO RMC14 30 second delay to grabbing the next lesser drone role
        // TODO RMC14 hive specific
        var time = _timing.CurTime;
        var query = EntityQueryEnumerator<HivePylonComponent>();
        while (query.MoveNext(out var uid, out var core))
        {
            if (TryComp(uid, out GhostRoleMobSpawnerComponent? spawner))
                UpdateGhostRoles((uid, core, spawner));

            if (TryComp(uid, out DamageableComponent? damageable) &&
                damageable.TotalDamage > FixedPoint2.Zero &&
                time >= core.HealAt)
            {
                core.HealAt = time + core.HealEvery;
                var damage = -_rmcDamageable.DistributeTypesTotal((uid, damageable), core.Heal);
                _damageable.TryChangeDamage(uid, damage);
            }
        }
    }
}
