using System.Numerics;
using Content.Server.Atmos.Components;
using Content.Server.Spreader;
using Content.Shared._RMC14.Map;
using Content.Shared._RMC14.Xenonids.Construction;
using Content.Shared._RMC14.Xenonids.Construction.Nest;
using Content.Shared._RMC14.Xenonids.Hive;
using Content.Shared._RMC14.Xenonids.Weeds;
using Content.Shared.Atmos;
using Content.Shared.Coordinates;
using Content.Shared.Maps;
using Content.Shared.Tag;
using Robust.Server.GameObjects;
using Robust.Shared.Map.Components;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Server._RMC14.Xenonids.Weeds;

public sealed class XenoWeedsSystem : SharedXenoWeedsSystem
{
    [Dependency] private readonly MapSystem _mapSystem = default!;
    [Dependency] private readonly SharedXenoHiveSystem _hive = default!;
    [Dependency] private readonly MapSystem _map = default!;
    [Dependency] private readonly SharedRMCMapSystem _rmcMap = default!;
    [Dependency] private readonly TagSystem _tag = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly TransformSystem _transform = default!;
    [Dependency] private readonly EntityManager _entities = default!;
    [Dependency] private readonly AppearanceSystem _appearance = default!;

    private static readonly ProtoId<TagPrototype> IgnoredTag = "SpreaderIgnore";

    private readonly List<EntityUid> _anchored = new();
    private readonly List<Entity<XenoWeedsComponent>> _spread = new();

    private EntityQuery<AirtightComponent> _airtightQuery;
    private EntityQuery<MapGridComponent> _mapGridQuery;
    private EntityQuery<XenoNestSurfaceComponent> _xenoNestSurfaceQuery;
    private EntityQuery<XenoWeedableComponent> _xenoWeedableQuery;
    private EntityQuery<XenoWeedsComponent> _xenoWeedsQuery;

    public override void Initialize()
    {
        base.Initialize();

        _airtightQuery = GetEntityQuery<AirtightComponent>();
        _mapGridQuery = GetEntityQuery<MapGridComponent>();
        _xenoNestSurfaceQuery = GetEntityQuery<XenoNestSurfaceComponent>();
        _xenoWeedableQuery = GetEntityQuery<XenoWeedableComponent>();
        _xenoWeedsQuery = GetEntityQuery<XenoWeedsComponent>();

        SubscribeLocalEvent<ReplaceWeedSourceOnWeedingComponent, AfterEntityWeedingEvent>(OnWeedOver);
    }

    private void OnWeedOver(Entity<ReplaceWeedSourceOnWeedingComponent> weedSource, ref AfterEntityWeedingEvent args)
    {
        var (ent, comp) = weedSource;
        var weededEntity = _entities.GetEntity(args.CoveredEntity);

        if (!TryComp(ent, out XenoWeedsComponent? weedComp) ||
            Prototype(weededEntity) is not EntityPrototype weededEntityProto ||
            !comp.ReplacementPairs.TryGetValue(weededEntityProto.ID, out var replacementId))
        {
            return;
        }

        var newWeedSource = SpawnAtPosition(replacementId, weedSource.Owner.ToCoordinates());
        if (!TryComp(newWeedSource, out XenoWeedsComponent? newWeedSourceComp))
        {
            QueueDel(newWeedSource);
            return;
        }

        _hive.SetSameHive(ent, newWeedSource);

        var curWeeds = weedComp.Spread;
        foreach (var curWeed in curWeeds)
        {
            var curWeedComp = EnsureComp<XenoWeedsComponent>(curWeed);
            curWeedComp.Range = newWeedSourceComp.Range;
            curWeedComp.Source = newWeedSource;
            newWeedSourceComp.Spread.Add(curWeed);
        }
        curWeeds.Clear();
        RemComp<XenoWeedsSpreadingComponent>(newWeedSource);
        QueueDel(ent);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        _spread.Clear();

        var time = _timing.CurTime;
        var spreadingQuery = EntityQueryEnumerator<XenoWeedsSpreadingComponent, XenoWeedsComponent>();
        while (spreadingQuery.MoveNext(out var uid, out var spreading, out var weeds))
        {
            if (time < spreading.SpreadAt)
                continue;

            RemCompDeferred<XenoWeedsSpreadingComponent>(uid);
            _spread.Add((uid, weeds));
        }

        foreach (var (uid, weeds) in _spread)
        {
            if (_transform.GetGrid(uid) is not { } gridId ||
                !_mapGridQuery.TryComp(gridId, out var gridComp))
            {
                continue;
            }

            var grid = new Entity<MapGridComponent>(gridId, gridComp);
            var indices = _map.CoordinatesToTile(gridId, gridComp, uid.ToCoordinates());
            foreach (var cardinal in _rmcMap.AtmosCardinalDirections)
            {
                var blocked = false;
                EntityUid? weedsToReplace = null;
                var neighbor = indices.Offset(cardinal);
                var anchored = _rmcMap.GetAnchoredEntitiesEnumerator(grid, neighbor);
                while (anchored.MoveNext(out var anchoredId))
                {
                    if (_airtightQuery.TryGetComponent(anchoredId, out var airtight) &&
                        airtight.AirBlocked &&
                        !_tag.HasTag(anchoredId, IgnoredTag))
                    {
                        blocked = true;
                        continue;
                    }

                    if (!_map.TryGetTileRef(grid, grid, neighbor, out var tileRef) ||
                        tileRef.Tile.IsEmpty ||
                        tileRef.Tile.IsSpace())
                    {
                        blocked = true;
                        continue;
                    }

                    if (_xenoWeedsQuery.TryComp(anchoredId, out var otherWeeds))
                    {
                        if (otherWeeds.Level >= weeds.Level)
                            blocked = true;
                        else
                            weedsToReplace = anchoredId;
                    }
                }

                if (blocked)
                    continue;

                // TODO RMC14
                // There is an edge case right now where existing weeds can block new weeds
                // from expanding further. If this is the case then the weeds should reassign
                // their source to this one and reactivate if it is closer to them than their
                // original source and only if it is still within range
                var source = weeds.IsSource ? uid : weeds.Source;
                var sourceWeeds = CompOrNull<XenoWeedsComponent>(source);

                if (source != null && sourceWeeds != null)
                    Dirty(source.Value, sourceWeeds);

                if (!Exists(source) ||
                    !TryComp(source, out TransformComponent? transform) ||
                    weeds.Spawns.Id is not { } prototype)
                {
                    continue;
                }

                var sourceLocal = _mapSystem.CoordinatesToTile(grid, gridComp, transform.Coordinates);
                var diff = Vector2.Abs(neighbor - sourceLocal);
                if (diff.X >= weeds.Range || diff.Y >= weeds.Range)
                    break;

                if (!CanPlaceWeedsPopup(grid, neighbor, null, weeds.SpreadsOnSemiWeedable))
                    continue;

                if (weedsToReplace != null)
                    QueueDel(weedsToReplace.Value);

                var coords = _mapSystem.GridTileToLocal(grid, grid, neighbor);
                var neighborWeeds = Spawn(prototype, coords);
                var neighborWeedsComp = EnsureComp<XenoWeedsComponent>(neighborWeeds);

                neighborWeedsComp.IsSource = false;
                neighborWeedsComp.Source = source;
                sourceWeeds?.Spread.Add(neighborWeeds);

                _hive.SetSameHive(uid, neighborWeeds);

                Dirty(neighborWeeds, neighborWeedsComp);

                EnsureComp<ActiveEdgeSpreaderComponent>(neighborWeeds);

                for (var i = 0; i < 4; i++)
                {
                    var dir = (AtmosDirection)(1 << i);
                    var pos = neighbor.Offset(dir);
                    if (!_mapSystem.TryGetTileRef(grid, grid, pos, out var adjacent))
                        continue;

                    _anchored.Clear();
                    _mapSystem.GetAnchoredEntities(grid, adjacent.GridIndices, _anchored);
                    foreach (var anchoredId in _anchored)
                    {
                        if (!_xenoWeedableQuery.TryComp(anchoredId, out var weedable) ||
                            !TryComp(anchoredId, out TransformComponent? weedableTransform) ||
                            !weedableTransform.Anchored)
                        {
                            continue;
                        }

                        var ev = new AfterEntityWeedingEvent(_entities.GetNetEntity(neighborWeeds), _entities.GetNetEntity(anchoredId));
                        RaiseLocalEvent(anchoredId, ev);

                        if (source is not null)
                            RaiseLocalEvent(source.Value, ev);

                        neighborWeedsComp.LocalWeeded.Add(anchoredId);
                        _appearance.SetData(anchoredId, WeededEntityLayers.Layer, true);

                        if (weedable.Spawn is null)
                        {
                            continue;
                        }

                        weedable.Entity = SpawnAtPosition(weedable.Spawn, anchoredId.ToCoordinates());
                        if (_xenoNestSurfaceQuery.TryComp(weedable.Entity, out var surface))
                        {
                            surface.Weedable = anchoredId;
                            Dirty(weedable.Entity.Value, surface);
                        }

                        sourceWeeds?.Spread.Add(weedable.Entity.Value);
                    }
                }
            }
        }
    }
}
