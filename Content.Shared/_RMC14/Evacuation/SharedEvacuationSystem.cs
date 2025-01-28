using System.Linq;
using System.Numerics;
using System.Text;
using Content.Shared._RMC14.Areas;
using Content.Shared._RMC14.Dropship;
using Content.Shared._RMC14.Explosion;
using Content.Shared._RMC14.Map;
using Content.Shared._RMC14.Marines.Announce;
using Content.Shared._RMC14.Marines.HyperSleep;
using Content.Shared._RMC14.Power;
using Content.Shared._RMC14.Xenonids.Announce;
using Content.Shared.CCVar;
using Content.Shared.Coordinates;
using Content.Shared.Doors;
using Content.Shared.Doors.Components;
using Content.Shared.Doors.Systems;
using Content.Shared.Examine;
using Content.Shared.GameTicking;
using Content.Shared.Mobs.Components;
using Content.Shared.Popups;
using Content.Shared.UserInterface;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Configuration;
using Robust.Shared.Containers;
using Robust.Shared.Map;
using Robust.Shared.Network;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Timing;

namespace Content.Shared._RMC14.Evacuation;

public abstract class SharedEvacuationSystem : EntitySystem
{
    [Dependency] private readonly SharedAppearanceSystem _appearance = default!;
    [Dependency] private readonly AreaSystem _area = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly IConfigurationManager _config = default!;
    [Dependency] private readonly SharedContainerSystem _container = default!;
    [Dependency] private readonly SharedDoorSystem _door = default!;
    [Dependency] private readonly SharedHyperSleepChamberSystem _hyperSleep = default!;
    [Dependency] private readonly SharedMapSystem _mapSystem = default!;
    [Dependency] private readonly SharedMarineAnnounceSystem _marineAnnounce = default!;
    [Dependency] private readonly INetManager _net = default!;
    [Dependency] private readonly SharedRMCExplosionSystem _rmcExplosion = default!;
    [Dependency] private readonly SharedRMCMapSystem _rmcMap = default!;
    [Dependency] private readonly SharedRMCPowerSystem _rmcPower = default!;
    [Dependency] private readonly SharedPhysicsSystem _physics = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly SharedUserInterfaceSystem _ui = default!;
    [Dependency] private readonly SharedXenoAnnounceSystem _xenoAnnounce = default!;

    private EntityQuery<AreaComponent> _areaQuery;
    private EntityQuery<DoorComponent> _doorQuery;
    private EntityQuery<MobStateComponent> _mobStateQuery;

    private MapId? _map;
    private int _index;

    public override void Initialize()
    {
        _areaQuery = GetEntityQuery<AreaComponent>();
        _doorQuery = GetEntityQuery<DoorComponent>();
        _mobStateQuery = GetEntityQuery<MobStateComponent>();

        SubscribeLocalEvent<DropshipHijackLandedEvent>(OnDropshipHijackLanded, after: [typeof(SharedRMCPowerSystem)]);
        SubscribeLocalEvent<EvacuationEnabledEvent>(OnEvacuationEnabled);
        SubscribeLocalEvent<EvacuationDisabledEvent>(OnEvacuationDisabled);
        SubscribeLocalEvent<EvacuationProgressEvent>(OnEvacuationProgress);
        SubscribeLocalEvent<RoundRestartCleanupEvent>(OnRoundRestartCleanup);

        SubscribeLocalEvent<GridSpawnerComponent, MapInitEvent>(OnGridSpawnerMapInit);

        SubscribeLocalEvent<EvacuationDoorComponent, BeforeDoorOpenedEvent>(OnEvacuationDoorBeforeOpened);
        SubscribeLocalEvent<EvacuationDoorComponent, BeforeDoorClosedEvent>(OnEvacuationDoorBeforeClosed);

        SubscribeLocalEvent<EvacuationComputerComponent, ExaminedEvent>(OnEvacuationComputerExamined);
        SubscribeLocalEvent<EvacuationComputerComponent, ActivatableUIOpenAttemptEvent>(OnEvacuationComputerUIOpenAttempt);

        SubscribeLocalEvent<LifeboatComputerComponent, ActivatableUIOpenAttemptEvent>(OnLifeboatComputerUIOpenAttempt);

        SubscribeLocalEvent<EvacuationPumpComponent, ExaminedEvent>(OnEvacuationPumpExamined);

        Subs.BuiEvents<EvacuationComputerComponent>(EvacuationComputerUi.Key,
            subs =>
            {
                subs.Event<EvacuationComputerLaunchBuiMsg>(OnEvacuationComputerLaunch);
            });

        Subs.BuiEvents<LifeboatComputerComponent>(LifeboatComputerUi.Key,
            subs =>
            {
                subs.Event<LifeboatComputerLaunchBuiMsg>(OnLifeboatComputerLaunch);
            });
    }

    private void OnDropshipHijackLanded(ref DropshipHijackLandedEvent ev)
    {
        EnsureComp<EvacuationProgressComponent>(ev.Map);

        var doors = EntityQueryEnumerator<EvacuationDoorComponent>();
        while (doors.MoveNext(out var uid, out var door))
        {
            door.Locked = false;
            Dirty(uid, door);
        }

        _config.SetCVar(CCVars.GameDisallowLateJoins, true);
    }

    private void OnEvacuationEnabled(ref EvacuationEnabledEvent ev)
    {
        var lifeboats = EntityQueryEnumerator<LifeboatComputerComponent>();
        while (lifeboats.MoveNext(out var uid, out var computer))
        {
            computer.Enabled = true;
            Dirty(uid, computer);
        }

        var evacuation = EntityQueryEnumerator<EvacuationComputerComponent>();
        while (evacuation.MoveNext(out var computerId, out var computer))
        {
            if (computer.Mode == EvacuationComputerMode.Disabled)
            {
                computer.Mode = EvacuationComputerMode.Ready;
                Dirty(computerId, computer);
            }
        }
    }

    private void OnEvacuationDisabled(ref EvacuationDisabledEvent ev)
    {
        var lifeboats = EntityQueryEnumerator<LifeboatComputerComponent>();
        while (lifeboats.MoveNext(out var uid, out var computer))
        {
            computer.Enabled = false;
            Dirty(uid, computer);
        }
    }

    private void OnEvacuationProgress(ref EvacuationProgressEvent ev)
    {
        var evacuation = EntityQueryEnumerator<EvacuationComputerComponent>();
        while (evacuation.MoveNext(out var computerId, out var computer))
        {
            if (computer.Mode == EvacuationComputerMode.Disabled)
            {
                computer.Mode = EvacuationComputerMode.Ready;
                Dirty(computerId, computer);
            }
        }
    }

    private void OnRoundRestartCleanup(RoundRestartCleanupEvent ev)
    {
        _map = null;
        _index = 0;
    }

    private void OnGridSpawnerMapInit(Entity<GridSpawnerComponent> ent, ref MapInitEvent args)
    {
        if (ent.Comp.Spawn is not { } spawn)
            return;

        if (_net.IsClient)
            return;

        if (_map == null)
        {
            _mapSystem.CreateMap(out var mapId);
            _map = mapId;
        }

        var matrix = Matrix3x2.CreateTranslation(_index * 50, _index * 50);
        _index++;

        _rmcMap.TryLoad(_map.Value, spawn.ToString(), out var grids, matrix);
        if (grids == null || grids.Count == 0)
            return;

        var grid = grids[0];
        var xform = Transform(ent);
        var coordinates = _transform.GetMapCoordinates(ent, xform);
        coordinates = coordinates.Offset(ent.Comp.Offset);
        _transform.SetMapCoordinates(grid, coordinates);

        if (TryComp(grid, out PhysicsComponent? physics) &&
            TryComp(grid, out FixturesComponent? fixtures))
        {
            _physics.SetBodyType(grid, BodyType.Static, manager: fixtures, body: physics);
            _physics.SetBodyStatus(grid, physics, BodyStatus.OnGround);
            _physics.SetFixedRotation(grid, true, manager: fixtures, body: physics);
        }
    }

    private void OnEvacuationDoorBeforeOpened(Entity<EvacuationDoorComponent> ent, ref BeforeDoorOpenedEvent args)
    {
        if (args.Cancelled)
            return;

        if (ent.Comp.Locked)
            args.Cancel();
    }

    private void OnEvacuationDoorBeforeClosed(Entity<EvacuationDoorComponent> ent, ref BeforeDoorClosedEvent args)
    {
        if (ent.Comp.Locked)
            args.PerformCollisionCheck = false;
    }

    private void OnEvacuationComputerExamined(Entity<EvacuationComputerComponent> ent, ref ExaminedEvent args)
    {
        if (ent.Comp.MaxMobs is { } maxMobs)
        {
            using (args.PushGroup(nameof(EvacuationComputerComponent)))
            {
                args.PushMarkup(Loc.GetString("rmc-evacuation-computer-max-occupants", ("count", maxMobs)));
            }
        }
    }

    private void OnEvacuationComputerUIOpenAttempt(Entity<EvacuationComputerComponent> ent, ref ActivatableUIOpenAttemptEvent args)
    {
        if (args.Cancelled || ent.Comp.Mode == EvacuationComputerMode.Ready)
            return;

        args.Cancel();

        var msg = ent.Comp.Mode switch
        {
            EvacuationComputerMode.Disabled => Loc.GetString("rmc-evacuation-computer-not-ready"),
            EvacuationComputerMode.Ready => "",
            EvacuationComputerMode.Travelling => Loc.GetString("rmc-evacuation-computer-launched"),
            EvacuationComputerMode.Crashed => Loc.GetString("rmc-evacuation-computer-crashed"),
            _ => throw new ArgumentOutOfRangeException(),
        };

        _popup.PopupClient(msg, ent, args.User, PopupType.SmallCaution);
    }

    private void OnEvacuationPumpExamined(Entity<EvacuationPumpComponent> ent, ref ExaminedEvent args)
    {
        if (!IsEvacuationInProgress())
            return;
        using (args.PushGroup(nameof(EvacuationPumpComponent)))
        {
            var progress = GetEvacuationProgress();
            if (progress < 25)
                args.PushMarkup(Loc.GetString("rmc-evacuation-pump-no-fuel"));
            else if (progress < 50)
                args.PushMarkup(Loc.GetString("rmc-evacuation-pump-some-fuel"));
            else if (progress < 75)
                args.PushMarkup(Loc.GetString("rmc-evacuation-pump-half-fuel"));
            else if (progress < 100)
                args.PushMarkup(Loc.GetString("rmc-evacuation-pump-almost-full"));
            else
                args.PushMarkup(Loc.GetString("rmc-evacuation-pump-full"));
        }
    }

    private void OnLifeboatComputerUIOpenAttempt(Entity<LifeboatComputerComponent> ent, ref ActivatableUIOpenAttemptEvent args)
    {
        if (args.Cancelled || ent.Comp.Enabled)
            return;

        args.Cancel();
        _popup.PopupClient(Loc.GetString("rmc-evacuation-lifeboat-not-authorized"), ent, args.User, PopupType.SmallCaution);
    }

    private void OnEvacuationComputerLaunch(Entity<EvacuationComputerComponent> ent, ref EvacuationComputerLaunchBuiMsg args)
    {
        var user = args.Actor;
        if (ent.Comp.Mode != EvacuationComputerMode.Ready)
        {
            Log.Warning($"{ToPrettyString(user)} tried to activate evacuation computer {ToPrettyString(ent)} that is not ready. Mode: {ent.Comp.Mode}");
            return;
        }

        if (Transform(ent).GridUid is not { } gridId)
        {
            Log.Warning($"{ToPrettyString(user)} tried to activate evacuation computer {ToPrettyString(ent)} not on grid");
            return;
        }

        var gridTransform = Transform(gridId);
        if (ent.Comp.MaxMobs is { } maxMobs)
        {
            var mobs = 0;
            var children = gridTransform.ChildEnumerator;
            while (children.MoveNext(out var uid))
            {
                var mob = _mobStateQuery.HasComp(uid);
                if (!mob)
                {
                    if (TryComp(uid, out ContainerManagerComponent? containerManager))
                    {
                        foreach (var container in _container.GetAllContainers(uid, containerManager))
                        {
                            if (container.ContainedEntities.Any(_mobStateQuery.HasComp))
                            {
                                mob = true;
                                break;
                            }
                        }
                    }
                }

                if (mob)
                {
                    mobs++;

                    if (mobs > maxMobs && ent.Comp.Mode != EvacuationComputerMode.Crashed)
                    {
                        ent.Comp.Mode = EvacuationComputerMode.Crashed;
                        _popup.PopupClient(Loc.GetString("rmc-evacuation-computer-overload"), ent, user, PopupType.LargeCaution);

                        var time = _timing.CurTime;
                        var detonating = EnsureComp<DetonatingEvacuationComputerComponent>(ent);
                        detonating.DetonateAt = time + ent.Comp.DetonateDelay;
                        detonating.EjectAt = time + ent.Comp.EjectDelay;
                    }
                }

                if (_doorQuery.TryComp(uid, out var door))
                {
                    var evacuationDoor = EnsureComp<EvacuationDoorComponent>(uid);
                    evacuationDoor.Locked = true;
                    Dirty(uid, evacuationDoor);
                    _door.TryClose(uid, door);
                }
            }
        }

        if (ent.Comp.Mode == EvacuationComputerMode.Crashed)
            return;

        _audio.PlayPredicted(ent.Comp.WarmupSound, ent, user);
        ent.Comp.Mode = EvacuationComputerMode.Travelling;
        Dirty(ent);

        var crashChance = IsEvacuationComplete() ? 0 : ent.Comp.EarlyCrashChance;
        LaunchEvacuationFTL(gridId, crashChance, ent.Comp.LaunchSound);
    }

    private void OnLifeboatComputerLaunch(Entity<LifeboatComputerComponent> ent, ref LifeboatComputerLaunchBuiMsg args)
    {
        var user = args.Actor;
        if (!ent.Comp.Enabled)
        {
            Log.Warning($"{ToPrettyString(user)} tried to activate lifeboat computer {ToPrettyString(ent)} that is not ready.");
            return;
        }

        if (Transform(ent).GridUid is not { } gridId)
            return;

        ent.Comp.Enabled = false;
        Dirty(ent);

        var crashChance = IsEvacuationComplete() ? 0 : ent.Comp.EarlyCrashChance;
        LaunchEvacuationFTL(gridId, crashChance, null);
    }

    protected virtual void LaunchEvacuationFTL(EntityUid grid, float crashLandChance, SoundSpecifier? launchSound)
    {
    }

    private void SetPumpAppearance(EvacuationPumpVisuals visual)
    {
        var pumps = EntityQueryEnumerator<EvacuationPumpComponent>();
        while (pumps.MoveNext(out var uid, out _))
        {
            _appearance.SetData(uid, EvacuationPumpLayers.Layer, visual);
        }
    }

    private IEnumerable<EntityUid> GetEvacuationAreas(EntityCoordinates coordinates)
    {
        if (!_area.TryGetAllAreas(coordinates, out var areaGrid))
            yield break;

        foreach (var areaId in areaGrid.Value.Comp.AreaEntities.Values)
        {
            if (!_areaQuery.TryComp(areaId, out var area) ||
                !area.HijackEvacuationArea)
            {
                continue;
            }

            yield return areaId;
        }
    }

    private bool IsAreaPumpPowered(EntityUid area)
    {
        return _rmcPower.IsAreaPowered(area, RMCPowerChannel.Equipment);
    }

    public void ToggleEvacuation(SoundSpecifier? startSound, SoundSpecifier? cancelSound)
    {
        var query = EntityQueryEnumerator<EvacuationProgressComponent>();
        while (query.MoveNext(out var uid, out var progress))
        {
            progress.Enabled = !progress.Enabled;
            Dirty(uid, progress);

            if (progress.Enabled)
            {
                _marineAnnounce.AnnounceARES(
                    null,
                    Loc.GetString("rmc-evacuation-start-announcement"),
                    startSound
                );
                var ev = new EvacuationEnabledEvent();
                RaiseLocalEvent(uid, ref ev, true);
            }
            else
            {
                _marineAnnounce.AnnounceARES(
                    null,
                    Loc.GetString("rmc-evacuation-cancelled-announcement"),
                    cancelSound
                );
                var ev = new EvacuationDisabledEvent();
                RaiseLocalEvent(uid, ref ev, true);
            }

            break;
        }
    }

    public bool IsEvacuationInProgress()
    {
        var query = EntityQueryEnumerator<EvacuationProgressComponent>();
        while (query.MoveNext(out _))
        {
            return true;
        }

        return false;
    }

    public bool IsEvacuationEnabled()
    {
        var query = EntityQueryEnumerator<EvacuationProgressComponent>();
        while (query.MoveNext(out var progress))
        {
            if (progress.Enabled)
                return true;
        }

        return false;
    }

    public int GetEvacuationProgress()
    {
        var query = EntityQueryEnumerator<EvacuationProgressComponent>();
        while (query.MoveNext(out var progress))
        {
            return (int) progress.Progress;
        }

        return 0;
    }

    public bool IsEvacuationComplete()
    {
        return GetEvacuationProgress() >= 100;
    }

    private void ProcessEvacuation()
    {
        if (_net.IsClient)
            return;

        var time = _timing.CurTime;
        var query = EntityQueryEnumerator<EvacuationProgressComponent>();
        while (query.MoveNext(out var uid, out var progress))
        {
            if (!progress.StartAnnounced)
            {
                progress.StartAnnounced = true;
                SetPumpAppearance(EvacuationPumpVisuals.Empty);

                var areas = new StringBuilder();
                foreach (var areaId in GetEvacuationAreas(uid.ToCoordinates()))
                {
                    var powered = IsAreaPumpPowered(areaId);
                    var line = Loc.GetString("rmc-evacuation-goal-percents", ("area", areaId), ("powered", powered));
                    areas.AppendLine(line);
                }

                areas.Append(Loc.GetString("rmc-evacuation-progress-announcement"));
                _marineAnnounce.AnnounceARES(null, areas.ToString());
            }

            if (progress.NextUpdate > time)
                continue;

            progress.NextUpdate = time + progress.UpdateEvery;
            Dirty(uid, progress);

            double progressAdd = 0;
            double progressMultiply = 1;
            foreach (var areaId in GetEvacuationAreas(uid.ToCoordinates()))
            {
                if (!_areaQuery.TryComp(areaId, out var area) ||
                    !area.HijackEvacuationArea)
                {
                    continue;
                }

                var powered = IsAreaPumpPowered(areaId);
                if (progress.LastPower.TryGetValue(areaId, out var lastPower) &&
                    lastPower != powered)
                {
                    _marineAnnounce.AnnounceARES(null, Loc.GetString("rmc-evacuation-goal-percents", ("area", areaId), ("powered", powered)));
                }

                progress.LastPower[areaId] = powered;
                if (!powered)
                    continue;

                switch (area.HijackEvacuationType)
                {
                    case AreaHijackEvacuationType.Add:
                        progressAdd += area.HijackEvacuationWeight;
                        break;
                    case AreaHijackEvacuationType.Multiply:
                        progressMultiply += area.HijackEvacuationWeight;
                        break;
                    default:
                        continue;
                }
            }

            progress.Progress = Math.Min(progress.Required, progress.Progress + progressAdd * progressMultiply);

            if (progress.Progress >= progress.NextAnnounce)
            {
                var current = progress.NextAnnounce;
                progress.NextAnnounce = current + progress.AnnounceEvery;

                var onAreas = string.Join(", ",
                    progress.LastPower.Where(kvp => kvp.Value).Select(kvp => Name(kvp.Key)));
                var offAreas = string.Join(", ",
                    progress.LastPower.Where(kvp => !kvp.Value).Select(kvp => Name(kvp.Key)));

                string MarinePercentageString(int percentage)
                {
                    var marineAnnounce = Loc.GetString("rmc-evacuation-goal-percents", ("percentage", percentage));
                    if (offAreas.Length == 0)
                        marineAnnounce += Loc.GetString("rmc-evacuation-all-areas-operational");
                    else
                        marineAnnounce += Loc.GetString("rmc-evacuation-restore-power-areas", ("areas", offAreas));

                    return marineAnnounce;
                }

                if (progress.Progress >= progress.Required)
                {
                    _marineAnnounce.AnnounceARES(null, Loc.GetString("rmc-evacuation-goal-100-percent"));
                    _xenoAnnounce.AnnounceAll(default, Loc.GetString("rmc-xeno-evacuation-goal-100-percent"));
                    SetPumpAppearance(EvacuationPumpVisuals.Full);
                    var ev = new EvacuationProgressEvent(100);
                    RaiseLocalEvent(uid, ref ev, true);
                }
                else if (progress.Progress >= progress.Required * 0.75)
                {
                    _marineAnnounce.AnnounceARES(null, MarinePercentageString(75));

                    var xenoAnnounce = Loc.GetString("rmc-xeno-evacuation-goal-75-percent");
                    if (onAreas.Length > 0)
                        xenoAnnounce += Loc.GetString("rmc-xeno-evacuation-disable-areas", ("areas", onAreas));

                    _xenoAnnounce.AnnounceAll(default, xenoAnnounce);
                    SetPumpAppearance(EvacuationPumpVisuals.SeventyFive);

                    var ev = new EvacuationProgressEvent(75);
                    RaiseLocalEvent(uid, ref ev, true);
                }
                else if (progress.Progress >= progress.Required * 0.5)
                {
                    _marineAnnounce.AnnounceARES(null, MarinePercentageString(50));

                    var xenoAnnounce = Loc.GetString("rmc-xeno-evacuation-goal-50-percent");
                    if (onAreas.Length > 0)
                        xenoAnnounce += Loc.GetString("rmc-xeno-evacuation-disable-areas", ("areas", onAreas));

                    _xenoAnnounce.AnnounceAll(default, xenoAnnounce);
                    SetPumpAppearance(EvacuationPumpVisuals.Fifty);
                    var ev = new EvacuationProgressEvent(50);
                    RaiseLocalEvent(uid, ref ev, true);
                }
                else if (progress.Progress >= progress.Required * 0.25)
                {
                    var marineAnnounce = Loc.GetString("rmc-evacuation-goal-25-percent");
                    if (offAreas.Length == 0)
                        marineAnnounce += Loc.GetString("rmc-evacuation-all-areas-operational");
                    else
                        marineAnnounce += Loc.GetString("rmc-evacuation-restore-power-areas", ("areas", offAreas));

                    _marineAnnounce.AnnounceARES(null, marineAnnounce);

                    var xenoAnnounce = Loc.GetString("rmc-xeno-evacuation-goal-25-percent");
                    if (onAreas.Length > 0)
                        xenoAnnounce += Loc.GetString("rmc-xeno-evacuation-disable-areas", ("areas", onAreas));

                    _xenoAnnounce.AnnounceAll(default, xenoAnnounce);

                    SetPumpAppearance(EvacuationPumpVisuals.TwentyFive);
                    var ev = new EvacuationProgressEvent(25);
                    RaiseLocalEvent(uid, ref ev, true);
                }
            }
        }
    }

    private void ProcessExplodingPods()
    {
        if (_net.IsClient)
            return;

        var time = _timing.CurTime;
        var query = EntityQueryEnumerator<DetonatingEvacuationComputerComponent>();
        while (query.MoveNext(out var uid, out var detonating))
        {
            if (!detonating.Detonated && time >= detonating.DetonateAt)
            {
                detonating.Detonated = true;
                Dirty(uid, detonating);

                _rmcExplosion.TriggerExplosive(uid, delete: false);
            }

            if (!detonating.Ejected && time >= detonating.EjectAt)
            {
                detonating.Ejected = true;
                Dirty(uid, detonating);

                if (Transform(uid).GridUid is { } grid)
                {
                    var children = Transform(grid).ChildEnumerator;
                    while (children.MoveNext(out var child))
                    {
                        _hyperSleep.EjectChamber(child);

                        if (_doorQuery.TryComp(uid, out var door))
                        {
                            var evacuationDoor = EnsureComp<EvacuationDoorComponent>(uid);
                            evacuationDoor.Locked = true;
                            Dirty(uid, evacuationDoor);
                            _door.TryClose(uid, door);
                        }
                    }
                }
            }

            if (detonating.Detonated && detonating.Ejected)
                RemCompDeferred<DetonatingEvacuationComputerComponent>(uid);
        }
    }

    public override void Update(float frameTime)
    {
        ProcessEvacuation();
        ProcessExplodingPods();
    }
}
