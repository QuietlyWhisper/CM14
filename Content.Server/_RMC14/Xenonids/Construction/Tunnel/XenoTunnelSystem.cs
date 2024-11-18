using Content.Shared._RMC14.Stun;
using Content.Shared._RMC14.Xenonids;
using Content.Shared._RMC14.Xenonids.Construction;
using Content.Shared._RMC14.Xenonids.Construction.ResinHole;
using Content.Shared._RMC14.Xenonids.Construction.Tunnel;
using Content.Shared._RMC14.Xenonids.Egg;
using Content.Shared._RMC14.Xenonids.Hive;
using Content.Shared._RMC14.Xenonids.Plasma;
using Content.Shared._RMC14.Xenonids.Weeds;
using Content.Shared.Actions;
using Content.Shared.Coordinates;
using Content.Shared.Coordinates.Helpers;
using Content.Shared.Database;
using Content.Shared.DoAfter;
using Content.Shared.Interaction;
using Content.Shared.Movement.Events;
using Content.Shared.Popups;
using Content.Shared.Verbs;
using Robust.Shared.Containers;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Serialization;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Content.Server._RMC14.Xenonids.Construction.Tunnel;

public sealed partial class XenoTunnelSystem : SharedXenoTunnelSystem
{
    //private const string TunnelPrototypeId = "XenoTunnel";

    [Dependency] private readonly SharedDoAfterSystem _doAfter = default!;
    [Dependency] private readonly EntityManager _entities = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly SharedContainerSystem _container = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly XenoPlasmaSystem _xenoPlasma = default!;
    [Dependency] private readonly SharedXenoWeedsSystem _xenoWeeds = default!;
    [Dependency] private readonly SharedXenoResinHoleSystem _xenoResinHole = default!;
    [Dependency] private readonly SharedXenoConstructionSystem _xenoConstruct = default!;
    [Dependency] private readonly SharedUserInterfaceSystem _ui = default!;
    [Dependency] private readonly SharedActionsSystem _action = default!;
    public int NextTempTunnelId
    { get; private set; }
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<XenoComponent, XenoDigTunnelActionEvent>(OnCreateTunnel);
        SubscribeLocalEvent<XenoComponent, XenoPlaceResinTunnelDestroyWeedSourceDoAfterEvent>(OnCompleteRemoveWeedSource);
        SubscribeLocalEvent<XenoComponent, XenoDigTunnelDoAfter>(OnFinishCreateTunnel);

        SubscribeLocalEvent<XenoTunnelComponent, InteractHandEvent>(OnInteract);
        SubscribeLocalEvent<XenoTunnelComponent, ContainerRelayMovementEntityEvent>(OnAttemptMoveInTunnel);
        SubscribeLocalEvent<XenoTunnelComponent, TraverseXenoTunnelMessage>(OnMoveThroughTunnel);

        SubscribeLocalEvent<XenoTunnelComponent, EnterXenoTunnelDoAfterEvent>(OnFinishEnterTunnel);
        SubscribeLocalEvent<XenoTunnelComponent, TraverseXenoTunnelDoAfterEvent>(OnFinishMoveThroughTunnel);

        SubscribeLocalEvent<XenoTunnelComponent, OpenBoundInterfaceMessage>(GetAllAvailableTunnels);
        SubscribeLocalEvent<XenoTunnelComponent, NameTunnelMessage>(OnNameTunnel);

        SubscribeLocalEvent<XenoTunnelComponent, GetVerbsEvent<ActivationVerb>>(OnGetRenameVerb);
    }

    private void OnCreateTunnel(Entity<XenoComponent> xenoBuilder, ref XenoDigTunnelActionEvent args)
    {
        if (args.Handled)
        {
            return;
        }

        var location = _transform.GetMoverCoordinates(xenoBuilder).SnapToGrid(_entities);
        if (!CanPlaceTunnel(args.Performer, location))
        {
            return;
        }

        if (_transform.GetGrid(location) is not { } gridId ||
            !TryComp(gridId, out MapGridComponent? grid))
        {
            return;
        }

        if (!_xenoPlasma.HasPlasmaPopup(xenoBuilder.Owner, args.PlasmaCost, false))
        {
            return;
        }

        if (_xenoWeeds.GetWeedsOnFloor((gridId, grid), location, true) is EntityUid weedSource)
        {
            XenoPlaceResinTunnelDestroyWeedSourceDoAfterEvent weedRemovalEv = new()
            {
                CreateTunnelDelay = args.CreateTunnelDelay,
                PlasmaCost = args.PlasmaCost,
                Prototype = args.Prototype
            };

            var doAfterWeedRemovalArgs = new DoAfterArgs(EntityManager, xenoBuilder.Owner, args.DestroyWeedSourceDelay, weedRemovalEv, xenoBuilder.Owner, weedSource)
            {
                BlockDuplicate = true,
                BreakOnMove = true,
                DuplicateCondition = DuplicateConditions.SameTarget
            };
            _doAfter.TryStartDoAfter(doAfterWeedRemovalArgs);
            _popup.PopupEntity(Loc.GetString("rmc-xeno-construction-resin-tunnel-uproot"), args.Performer, args.Performer);
            args.Handled = true;
            return;
        }
        _xenoPlasma.TryRemovePlasma(xenoBuilder.Owner, args.PlasmaCost);
        var createTunnelEv = new XenoDigTunnelDoAfter(args.Prototype, args.PlasmaCost);
        var doAfterTunnelCreationArgs = new DoAfterArgs(EntityManager, xenoBuilder.Owner, args.CreateTunnelDelay, createTunnelEv, xenoBuilder.Owner)
        {
            BlockDuplicate = true,
            BreakOnMove = true,
            DuplicateCondition = DuplicateConditions.SameTarget
        };
        _doAfter.TryStartDoAfter(doAfterTunnelCreationArgs);
        _popup.PopupEntity(Loc.GetString("rmc-xeno-construction-resin-tunnel-create-tunnel"), args.Performer, args.Performer);
        args.Handled = true;
    }

    private void OnCompleteRemoveWeedSource(Entity<XenoComponent> xenoBuilder, ref XenoPlaceResinTunnelDestroyWeedSourceDoAfterEvent args)
    {
        if (args.Cancelled)
        {
            var actions = _action.GetActions(xenoBuilder.Owner);
            foreach (var (action, comp) in actions)
            {
                if (comp is InstantActionComponent instantActionComp &&
                    instantActionComp.Event is XenoDigTunnelActionEvent)
                {
                    _action.ClearCooldown(action);
                    break;
                }
            }
        }

        if (args.Handled || args.Cancelled)
        {
            return;
        }

        if (args.Target is null)
        {
            return;
        }

        if (!_xenoPlasma.HasPlasmaPopup(xenoBuilder.Owner, args.PlasmaCost, false))
        {
            return;
        }

        QueueDel(args.Target);

        _xenoPlasma.TryRemovePlasma(xenoBuilder.Owner, args.PlasmaCost);
        var createTunnelEv = new XenoDigTunnelDoAfter(args.Prototype, args.PlasmaCost);
        var doAfterTunnelCreationArgs = new DoAfterArgs(EntityManager, xenoBuilder.Owner, args.CreateTunnelDelay, createTunnelEv, xenoBuilder.Owner)
        {
            BlockDuplicate = true,
            BreakOnMove = true,
            DuplicateCondition = DuplicateConditions.SameTarget
        };
        _doAfter.TryStartDoAfter(doAfterTunnelCreationArgs);
        _popup.PopupEntity(Loc.GetString("rmc-xeno-construction-resin-tunnel-create-tunnel"), xenoBuilder.Owner, xenoBuilder.Owner);
        args.Handled = true;
    }
    private void OnFinishCreateTunnel(Entity<XenoComponent> xenoBuilder, ref XenoDigTunnelDoAfter args)
    {
        if (args.Cancelled)
        {
            var actions = _action.GetActions(xenoBuilder.Owner);
            foreach (var (action, comp) in actions)
            {
                if (comp is InstantActionComponent instantActionComp &&
                    instantActionComp.Event is XenoDigTunnelActionEvent)
                {
                    _action.ClearCooldown(action);
                    break;
                }
            }
        }

        if (args.Handled || args.Cancelled)
        {
            return;
        }
        var tunnelFailureMessage = Loc.GetString("rmc-xeno-construction-failed-tunnel-rename");

        var location = _transform.GetMoverCoordinates(xenoBuilder).SnapToGrid(_entities);
        if (!CanPlaceTunnel(xenoBuilder.Owner, location))
        {
            _popup.PopupEntity(tunnelFailureMessage, xenoBuilder.Owner, xenoBuilder.Owner);
            return;
        }

        var newTunnelName = Loc.GetString("rmc-xeno-construction-default-tunnel-name", ("tunnelNumber", NextTempTunnelId));

        if (!TryPlaceTunnel(xenoBuilder.Owner, newTunnelName, out var newTunnelEnt))
        {
            _popup.PopupEntity(tunnelFailureMessage, xenoBuilder.Owner, xenoBuilder.Owner);
            return;
        }

        NextTempTunnelId++;
        _ui.OpenUi(newTunnelEnt.Value, NameTunnelUI.Key, xenoBuilder.Owner);

        args.Handled = true;
    }

    private void OnNameTunnel(Entity<XenoTunnelComponent> xenoTunnel, ref NameTunnelMessage args)
    {
        var name = args.TunnelName;
        var hive = _hive.GetHive(xenoTunnel.Owner);
        if (hive is null)
        {
            return;
        }
        var hiveComp = hive.Value.Comp;
        var hiveTunnels = hiveComp.HiveTunnels;

        string? curName = null;
        foreach (var item in hiveTunnels)
        {
            if (item.Value == xenoTunnel.Owner)
            {
                curName = item.Key;
            }
        }

        if (!hiveTunnels.TryAdd(name, xenoTunnel.Owner))
        {
            _popup.PopupCursor(Loc.GetString("rmc-xeno-construction-failed-tunnel-rename"), args.Actor);
            return;
        }

        if (curName is string)
        {
            hiveTunnels.Remove(curName);
        }

        _ui.CloseUi(xenoTunnel.Owner, NameTunnelUI.Key, args.Actor);
    }
    private void OnInteract(Entity<XenoTunnelComponent> xenoTunnel, ref InteractHandEvent args)
    {
        var (ent, comp) = xenoTunnel;
        if (args.Handled)
        {
            return;
        }

        var enteringEntity = args.User;

        if (_container.ContainsEntity(xenoTunnel.Owner, enteringEntity))
        {
            _ui.OpenUi(ent, SelectDestinationTunnelUI.Key, enteringEntity);
            return;
        }

        var mobContainer = _container.EnsureContainer<Container>(xenoTunnel.Owner, XenoTunnelComponent.ContainedMobsContainerId);
        if (!HasComp<XenoComponent>(enteringEntity))
        {
            if (mobContainer.Count == 0)
                _popup.PopupEntity(Loc.GetString("rmc-xeno-construction-tunnel-empty-non-xeno-enter-failure"), enteringEntity, enteringEntity);
            else
                _popup.PopupEntity(Loc.GetString("rmc-xeno-construction-tunnel-occupied-non-xeno-enter-failure"), enteringEntity, enteringEntity);
            return;
        }

        if (mobContainer.Count >= xenoTunnel.Comp.MaxMobs)
        {
            _popup.PopupEntity(Loc.GetString("rmc-xeno-construction-tunnel-full-xeno-enter-failure"), enteringEntity, enteringEntity);
            return;
        }

        if (!TryComp(enteringEntity, out RMCSizeComponent? xenoSize))
        {
            return;
        }

        var enterDelay = comp.StandardXenoEnterDelay;
        string? tunnelName;
        TryGetHiveTunnelName(xenoTunnel, out tunnelName);
        string? enterMessageLocID;
        switch (xenoSize.Size)
        {
            case RMCSizes.VerySmallXeno:
            case RMCSizes.Small:
                enterDelay = comp.SmallXenoEnterDelay;
                enterMessageLocID = "rmc-xeno-construction-tunnel-default-xeno-enter";
                break;
            case RMCSizes.Xeno:
                enterDelay = comp.StandardXenoEnterDelay;
                enterMessageLocID = "rmc-xeno-construction-tunnel-default-xeno-enter";
                break;
            case RMCSizes.Big:
            case RMCSizes.Immobile:
                enterDelay = comp.LargeXenoEnterDelay;
                enterMessageLocID = "rmc-xeno-construction-tunnel-large-xeno-enter";
                break;
            default:
                return;
        }

        if (tunnelName is string && enterMessageLocID is string)
        {
            _popup.PopupEntity(Loc.GetString(enterMessageLocID, ("tunnelName", tunnelName)), enteringEntity, enteringEntity);
        }

        var ev = new EnterXenoTunnelDoAfterEvent();
        var doAfterArgs = new DoAfterArgs(_entities, enteringEntity, enterDelay, ev, xenoTunnel.Owner)
        {
            BreakOnMove = true,

        };
        _doAfter.TryStartDoAfter(doAfterArgs);

        args.Handled = true;
    }
    private void OnMoveThroughTunnel(Entity<XenoTunnelComponent> xenoTunnel, ref TraverseXenoTunnelMessage args)
    {
        var (ent, comp) = xenoTunnel;

        var startingTunnel = _entities.GetEntity(args.Entity);
        var traversingXeno = args.Actor;

        // If the xeno leaves the tunnel, prevent teleportation
        if (!_container.ContainsEntity(startingTunnel, traversingXeno))
        {
            return;
        }

        var destinationTunnel = _entities.GetEntity(args.DestinationTunnel);
        if (!HasComp<XenoTunnelComponent>(destinationTunnel))
        {
            return;
        }


        if (!TryComp(traversingXeno, out RMCSizeComponent? xenoSize))
        {
            return;
        }

        var moveDelay = comp.StandardXenoMoveDelay;
        switch (xenoSize.Size)
        {
            case RMCSizes.VerySmallXeno:
            case RMCSizes.Small:
                moveDelay = comp.SmallXenoMoveDelay;
                break;
            case RMCSizes.Xeno:
                moveDelay = comp.StandardXenoMoveDelay;
                break;
            case RMCSizes.Big:
            case RMCSizes.Immobile:
                moveDelay = comp.LargeXenoMoveDelay;
                break;
            default:
                return;
        }

        var ev = new TraverseXenoTunnelDoAfterEvent();
        var doAfterArgs = new DoAfterArgs(_entities, traversingXeno, moveDelay, ev, destinationTunnel, null, xenoTunnel.Owner)
        {
            BreakOnMove = true
        };
        _doAfter.TryStartDoAfter(doAfterArgs);
    }
    private void OnAttemptMoveInTunnel(Entity<XenoTunnelComponent> xenoTunnel, ref ContainerRelayMovementEntityEvent args)
    {
        _transform.PlaceNextTo(args.Entity, xenoTunnel.Owner);
    }

    private void OnFinishEnterTunnel(Entity<XenoTunnelComponent> xenoTunnel, ref EnterXenoTunnelDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled)
        {
            return;
        }
        var (ent, comp) = xenoTunnel;
        var enteringEntity = args.User;

        var mobContainer = _container.EnsureContainer<Container>(ent, XenoTunnelComponent.ContainedMobsContainerId);
        _container.Insert(enteringEntity, mobContainer);
        _ui.OpenUi(ent, SelectDestinationTunnelUI.Key, enteringEntity);

        args.Handled = true;
    }

    private void OnFinishMoveThroughTunnel(Entity<XenoTunnelComponent> destinationXenoTunnel, ref TraverseXenoTunnelDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled)
        {
            return;
        }
        var (ent, comp) = destinationXenoTunnel;
        var traversingXeno = args.User;
        var startingTunnel = args.Used!.Value;

        // If the xeno leaves the tunnel, prevent teleportation
        if (!_container.ContainsEntity(startingTunnel, traversingXeno))
        {
            return;
        }

        var mobContainer = _container.EnsureContainer<Container>(ent, XenoTunnelComponent.ContainedMobsContainerId);

        _container.Insert(traversingXeno, mobContainer);
        _ui.OpenUi(destinationXenoTunnel.Owner, SelectDestinationTunnelUI.Key, args.User);

        args.Handled = true;
    }

    private void OnGetRenameVerb(Entity<XenoTunnelComponent> xenoTunnel, ref GetVerbsEvent<ActivationVerb> args)
    {
        var uid = args.User;

        var renameTunnelVerb = new ActivationVerb
        {
            Text = Loc.GetString("xeno-ui-rename-tunnel-verb"),
            Act = () =>
            {
                _ui.TryOpenUi(xenoTunnel.Owner, NameTunnelUI.Key, uid);
            },

            Impact = LogImpact.Low,
        };

        args.Verbs.Add(renameTunnelVerb);
    }
    private void GetAllAvailableTunnels(Entity<XenoTunnelComponent> destinationXenoTunnel, ref OpenBoundInterfaceMessage args)
    {
        var hive = _hive.GetHive(destinationXenoTunnel.Owner);
        if (hive is null ||
            !TryComp(hive, out HiveComponent? hiveComp))
        {
            return;
        }


        var hiveTunnels = hiveComp.HiveTunnels;
        Dictionary<string, NetEntity> netHiveTunnels = new();
        foreach (var (name, tunnel) in hiveTunnels)
        {
            netHiveTunnels.Add(name, _entities.GetNetEntity(tunnel));
        }

        var newState = new SelectDestinationTunnelInterfaceState(netHiveTunnels);

        _ui.SetUiState(destinationXenoTunnel.Owner, SelectDestinationTunnelUI.Key, newState);
    }
    private bool CanPlaceTunnel(EntityUid user, EntityCoordinates coords)
    {
        var canPlaceStructure = _xenoConstruct.CanPlaceXenoStructure(user, coords, out var popupType, false);

        if (!canPlaceStructure)
        {
            popupType = popupType + "-tunnel";
            _popup.PopupEntity(popupType, user, user, PopupType.SmallCaution);
            return false;
        }
        return true;
    }
    public bool TryPlaceTunnel(Entity<HiveMemberComponent?> builder, string name, [NotNullWhen(true)] out EntityUid? tunnelEnt)
    {
        tunnelEnt = null;
        if (!Resolve(builder, ref builder.Comp) ||
            builder.Comp.Hive is null)
        {
            return false;
        }

        return TryPlaceTunnel(builder.Comp.Hive.Value, name, builder.Owner.ToCoordinates(), out tunnelEnt);
    }
}
