﻿using Content.Shared._RMC14.NightVision;
using Content.Shared._RMC14.Xenonids.Announce;
using Content.Shared._RMC14.Xenonids.Evolution;
using Content.Shared.FixedPoint;
using Content.Shared.Mobs;
using Content.Shared.Popups;
using Robust.Shared.Map;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;
using Robust.Shared.Utility;

namespace Content.Shared._RMC14.Xenonids.Hive;

public abstract class SharedXenoHiveSystem : EntitySystem
{
    [Dependency] private readonly IComponentFactory _compFactory = default!;
    [Dependency] private readonly SharedNightVisionSystem _nightVision = default!;
    [Dependency] private readonly IPrototypeManager _prototypes = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly SharedXenoAnnounceSystem _xenoAnnounce = default!;

    private EntityQuery<HiveComponent> _query;
    private EntityQuery<HiveMemberComponent> _memberQuery;

    public override void Initialize()
    {
        base.Initialize();

        _query = GetEntityQuery<HiveComponent>();
        _memberQuery = GetEntityQuery<HiveMemberComponent>();

        SubscribeLocalEvent<HiveComponent, MapInitEvent>(OnMapInit);

        SubscribeLocalEvent<XenoEvolutionGranterComponent, MobStateChangedEvent>(OnGranterMobStateChanged);
    }

    private void OnGranterMobStateChanged(Entity<XenoEvolutionGranterComponent> ent, ref MobStateChangedEvent args)
    {
        if (args.NewMobState != MobState.Dead)
            return;

        if (GetHive(ent.Owner) is {} hive)
        {
            hive.Comp.LastQueenDeath = _timing.CurTime;
            Dirty(hive);
        }
    }

    private void OnMapInit(Entity<HiveComponent> ent, ref MapInitEvent args)
    {
        ent.Comp.AnnouncedUnlocks.Clear();
        ent.Comp.Unlocks.Clear();
        ent.Comp.AnnouncementsLeft.Clear();

        foreach (var prototype in _prototypes.EnumeratePrototypes<EntityPrototype>())
        {
            if (prototype.TryGetComponent(out XenoComponent? xeno, _compFactory))
            {
                if (xeno.UnlockAt == default)
                    continue;

                ent.Comp.Unlocks.GetOrNew(xeno.UnlockAt).Add(prototype.ID);

                if (!ent.Comp.AnnouncementsLeft.Contains(xeno.UnlockAt))
                    ent.Comp.AnnouncementsLeft.Add(xeno.UnlockAt);
            }
        }

        foreach (var unlock in ent.Comp.Unlocks)
        {
            unlock.Value.Sort();
        }

        ent.Comp.AnnouncementsLeft.Sort();
    }

    /// <summary>
    /// Tries to get the hive from a member, returning null if it has no hive or it is invalid.
    /// </summary>
    public Entity<HiveComponent>? GetHive(Entity<HiveMemberComponent?> member)
    {
        if (!_memberQuery.Resolve(member, ref member.Comp, false))
            return null;

        if (member.Comp.Hive is not {} uid || TerminatingOrDeleted(uid))
            return null;

        if (!_query.TryComp(uid, out var comp))
            return null;

        return (uid, comp);
    }

    /// <summary>
    /// Returns true if the entity has a valid hive, i.e. it isn't a rogue xeno.
    /// Only use this if you don't need to use the hive for anything after.
    /// </summary>
    public bool HasHive(Entity<HiveMemberComponent?> member)
    {
        return GetHive(member) != null;
    }

    /// <summary>
    /// Sets the hive for a member, if it's different.
    /// If it does not have HiveMemberComponent this method adds it.
    /// </summary>
    public void SetHive(Entity<HiveMemberComponent?> member, EntityUid? hive)
    {
        var comp = member.Comp ?? EnsureComp<HiveMemberComponent>(member);

        var old = comp.Hive;
        if (old == hive)
            return;

        Entity<HiveComponent>? hiveEnt = null;
        if (_query.TryComp(hive, out var hiveComp))
            hiveEnt = (hive.Value, hiveComp);
        else if (hive != null)
            return; // invalid hive was passed, prevent it breaking anything else

        comp.Hive = hive;
        Dirty(member, comp);

        var ev = new HiveChangedEvent(hiveEnt, old);
        RaiseLocalEvent(member, ref ev);
    }

    /// <summary>
    /// Sets the hive of the destination entity to that of the source entity, if it has one.
    /// If the source has no hive this is a no-op.
    /// If dest does not have HiveMemberComponent this method adds it like with <see cref="SetHive"/>.
    /// </summary>
    public void SetSameHive(Entity<HiveMemberComponent?> src, Entity<HiveMemberComponent?> dest)
    {
        if (GetHive(src) is {} hive)
            SetHive(dest, hive);
    }

    /// <summary>
    /// Returns true if the entities have the same hive and it isn't null.
    /// Rogue xenos don't have the same hive as they aren't in one!
    /// </summary>
    public bool FromSameHive(Entity<HiveMemberComponent?> a, Entity<HiveMemberComponent?> b)
    {
        if (GetHive(a) is not {} aHive)
            return false;

        return IsMember(b, aHive);
    }

    /// <summary>
    /// Returns true if the entity is a member of a specific hive.
    /// If the hive is null this always returns false, you cannot use this to check for rogue xenos.
    /// </summary>
    public bool IsMember(Entity<HiveMemberComponent?> member, EntityUid? hive)
    {
        if (hive == null || GetHive(member) is not {} memberHive)
            return false;

        return memberHive.Owner == hive;
    }

    public void SetSeeThroughContainers(Entity<HiveComponent?> hive, bool see)
    {
        if (!_query.Resolve(hive, ref hive.Comp, false))
            return;

        hive.Comp.SeeThroughContainers = see;
        var xenos = EntityQueryEnumerator<XenoComponent, HiveMemberComponent, NightVisionComponent>();
        while (xenos.MoveNext(out var uid, out _, out var member, out var nv))
        {
            if (member.Hive != hive)
                continue;

            _nightVision.SetSeeThroughContainers((uid, nv), see);
        }
    }

    public void AnnounceNeedsOvipositorToSameHive(Entity<HiveMemberComponent?> xeno)
    {
        if (GetHive(xeno) is not {} hive || hive.Comp.GotOvipositorPopup)
            return;

        hive.Comp.GotOvipositorPopup = true;
        Dirty(hive);

        // TODO: loc
        var msg = "Enough time has passed, we require the Queen in oviposition for evolution.";
        var xenos = EntityQueryEnumerator<XenoComponent, HiveMemberComponent, ActorComponent>();
        while (xenos.MoveNext(out var uid, out _, out var member, out _))
        {
            if (uid == xeno.Owner || member.Hive != hive)
                continue;

            _popup.PopupEntity(msg, uid, uid, PopupType.LargeCaution);
        }

        _xenoAnnounce.AnnounceToHive(default, hive, msg);
    }

    public bool TryGetTierLimit(Entity<HiveComponent?> hive, int tier, out FixedPoint2 value)
    {
        value = default;
        if (!_query.Resolve(hive, ref hive.Comp, false))
            return false;

        return hive.Comp.TierLimits.TryGetValue(tier, out value);
    }

    public bool TryGetFreeSlots(Entity<HiveComponent?> hive, EntProtoId caste, out int value)
    {
        value = default;
        if (!_query.Resolve(hive, ref hive.Comp, false))
            return false;

        return hive.Comp.FreeSlots.TryGetValue(caste, out value);
    }
}

/// <summary>
/// Raised on an entity after its hive is changed.
/// </summary>
[ByRefEvent]
public record struct HiveChangedEvent(Entity<HiveComponent>? Hive, EntityUid? OldHive);
