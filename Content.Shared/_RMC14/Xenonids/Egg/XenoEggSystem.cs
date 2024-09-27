using Content.Shared._RMC14.Hands;
using Content.Shared._RMC14.Marines;
using Content.Shared._RMC14.Xenonids.Construction;
using Content.Shared._RMC14.Xenonids.Parasite;
using Content.Shared._RMC14.Xenonids.Plasma;
using Content.Shared._RMC14.Xenonids.Weeds;
using Content.Shared.Actions;
using Content.Shared.Buckle.Components;
using Content.Shared.Database;
using Content.Shared.DoAfter;
using Content.Shared.FixedPoint;
using Content.Shared.Ghost;
using Content.Shared.Hands.Components;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction;
using Content.Shared.Interaction.Events;
using Content.Shared.Item;
using Content.Shared.Maps;
using Content.Shared.Mind;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Popups;
using Content.Shared.StepTrigger.Components;
using Content.Shared.StepTrigger.Systems;
using Content.Shared.Stunnable;
using Content.Shared.Tag;
using Content.Shared.Verbs;
using Content.Shared.Throwing;
using Robust.Shared.Map.Components;
using Robust.Shared.Network;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Serialization;
using Robust.Shared.Timing;
using static Content.Shared.Physics.CollisionGroup;
using Robust.Shared.Map;
using Robust.Shared.Audio.Systems;

namespace Content.Shared._RMC14.Xenonids.Egg;

public sealed class XenoEggSystem : EntitySystem
{
    [Dependency] private readonly SharedActionsSystem _actions = default!;
    [Dependency] private readonly SharedDoAfterSystem _doAfter = default!;
    [Dependency] private readonly SharedHandsSystem _hands = default!;
    [Dependency] private readonly SharedInteractionSystem _interaction = default!;
    [Dependency] private readonly SharedXenoParasiteSystem _parasite = default!;
    [Dependency] private readonly SharedMapSystem _map = default!;
    [Dependency] private readonly SharedMindSystem _mind = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly INetManager _net = default!;
    [Dependency] private readonly XenoPlasmaSystem _plasma = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly SharedStunSystem _stun = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly CMHandsSystem _rmcHands = default!;
    [Dependency] private readonly TagSystem _tags = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly TurfSystem _turf = default!;
    [Dependency] private readonly XenoSystem _xeno = default!;
    [Dependency] private readonly SharedUserInterfaceSystem _ui = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;

    private static readonly ProtoId<TagPrototype> AirlockTag = "Airlock";
    private static readonly ProtoId<TagPrototype> StructureTag = "Structure";

    private EntityQuery<StepTriggerComponent> _stepTriggerQuery;

    public override void Initialize()
    {
        _stepTriggerQuery = GetEntityQuery<StepTriggerComponent>();

        SubscribeLocalEvent<XenoComponent, XenoGrowOvipositorActionEvent>(OnXenoGrowOvipositorAction);
        SubscribeLocalEvent<XenoComponent, XenoGrowOvipositorDoAfterEvent>(OnXenoGrowOvipositorDoAfter);

        SubscribeLocalEvent<XenoAttachedOvipositorComponent, MapInitEvent>(OnXenoAttachedMapInit);
        SubscribeLocalEvent<XenoAttachedOvipositorComponent, ComponentRemove>(OnXenoAttachedRemove);
        SubscribeLocalEvent<XenoAttachedOvipositorComponent, MobStateChangedEvent>(OnXenoMobStateChanged);

        SubscribeLocalEvent<XenoEggComponent, AfterAutoHandleStateEvent>(OnXenoEggAfterState);
        SubscribeLocalEvent<XenoEggComponent, GettingPickedUpAttemptEvent>(OnXenoEggPickedUpAttempt);
        SubscribeLocalEvent<XenoEggComponent, InteractUsingEvent>(OnXenoEggInteractUsing);
        SubscribeLocalEvent<XenoEggComponent, AfterInteractEvent>(OnXenoEggAfterInteract);
        SubscribeLocalEvent<XenoEggComponent, ActivateInWorldEvent>(OnXenoEggActivateInWorld);
        SubscribeLocalEvent<XenoEggComponent, StepTriggerAttemptEvent>(OnXenoEggStepTriggerAttempt);
        SubscribeLocalEvent<XenoEggComponent, StepTriggeredOffEvent>(OnXenoEggStepTriggered);
        SubscribeLocalEvent<XenoEggComponent, GetVerbsEvent<ActivationVerb>>(OnGetVerbs);
        SubscribeLocalEvent<XenoEggComponent, ThrowItemAttemptEvent>(OnXenoEggTryThrow);
        SubscribeLocalEvent<XenoEggComponent, PlaceEggDoAfterEvent>(OnXenoEggDoAfter);
    }

    private void OnXenoGrowOvipositorAction(Entity<XenoComponent> xeno, ref XenoGrowOvipositorActionEvent args)
    {
        if (args.Handled)
            return;

        var hasOvipositor = HasComp<XenoAttachedOvipositorComponent>(xeno);
        if (!hasOvipositor &&
            !_plasma.HasPlasmaPopup(xeno.Owner, args.AttachPlasmaCost))
        {
            return;
        }

        args.Handled = true;

        var ev = new XenoGrowOvipositorDoAfterEvent { PlasmaCost = args.AttachPlasmaCost };
        var delay = args.AttachDoAfter;
        var popup = new LocId("cm-xeno-ovipositor-attach");
        var popupType = PopupType.Medium;
        if (hasOvipositor)
        {
            ev.PlasmaCost = FixedPoint2.Zero;
            delay = args.DetachDoAfter;
            popup = "cm-xeno-ovipositor-detach";
            popupType = PopupType.MediumCaution;
        }

        var doAfterArgs = new DoAfterArgs(EntityManager, xeno, delay, ev, xeno)
        {
            BreakOnMove = true,
        };

        if (_doAfter.TryStartDoAfter(doAfterArgs))
            _popup.PopupClient(Loc.GetString(popup), xeno, xeno, popupType);
    }

    private void OnXenoGrowOvipositorDoAfter(Entity<XenoComponent> xeno, ref XenoGrowOvipositorDoAfterEvent args)
    {
        if (args.Cancelled ||
            args.Handled ||
            !_plasma.TryRemovePlasmaPopup(xeno.Owner, args.PlasmaCost))
        {
            return;
        }

        args.Handled = true;

        if (TryComp(xeno, out XenoAttachedOvipositorComponent? attached))
            DetachOvipositor((xeno, attached));
        else
            AttachOvipositor(xeno.Owner);
    }

    private void OnXenoAttachedMapInit(Entity<XenoAttachedOvipositorComponent> attached, ref MapInitEvent args)
    {
        if (TryComp(attached, out TransformComponent? xform))
            _transform.AnchorEntity(attached, xform);

        var ev = new XenoOvipositorChangedEvent();
        RaiseLocalEvent(ref ev);
    }

    private void OnXenoAttachedRemove(Entity<XenoAttachedOvipositorComponent> attached, ref ComponentRemove args)
    {
        if (!TerminatingOrDeleted(attached) && TryComp(attached, out TransformComponent? xform))
            _transform.Unanchor(attached, xform);

        var ev = new XenoOvipositorChangedEvent();
        RaiseLocalEvent(ref ev);
    }

    private void OnXenoMobStateChanged(Entity<XenoAttachedOvipositorComponent> ent, ref MobStateChangedEvent args)
    {
        DetachOvipositor(ent);
    }

    private void OnXenoEggAfterState(Entity<XenoEggComponent> egg, ref AfterAutoHandleStateEvent args)
    {
        var ev = new XenoEggStateChangedEvent();
        RaiseLocalEvent(egg, ref ev);
    }

    private void OnXenoEggPickedUpAttempt(Entity<XenoEggComponent> egg, ref GettingPickedUpAttemptEvent args)
    {
        if (egg.Comp.State != XenoEggState.Item)
            args.Cancel();
    }

    private void OnXenoEggTryThrow(Entity<XenoEggComponent> ent, ref ThrowItemAttemptEvent args)
    {
        if (HasComp<XenoComponent>(args.User))
            _popup.PopupEntity(Loc.GetString("rmc-xeno-egg-throw-xeno"), args.User, args.User, PopupType.SmallCaution);
        else
            _popup.PopupEntity(Loc.GetString("rmc-xeno-egg-throw"), args.User, args.User, PopupType.SmallCaution);
        args.Cancelled = true;
    }

    private void OnXenoEggAfterInteract(Entity<XenoEggComponent> egg, ref AfterInteractEvent args)
    {
        if (args.Handled)
            return;

        if (egg.Comp.State != XenoEggState.Item ||
            !TryComp(egg, out TransformComponent? xform))
        {
            return;
        }

        bool handle = args.Handled;

        if (!CanPlaceEgg(egg, args.User, args.CanReach, args.ClickLocation, ref handle))
        {
            args.Handled = handle;
            return;
        }

        var plantTime = TimeSpan.FromSeconds(3.5);
        if (TryComp<EggPlantTimeComponent>(args.User, out var time))
            plantTime = time.PlantTime;

        var ev = new PlaceEggDoAfterEvent(GetNetCoordinates(args.ClickLocation));
        var doAfter = new DoAfterArgs(EntityManager, args.User, plantTime, ev, egg)
        {
            BreakOnMove = true,
            BlockDuplicate = true
        };

        args.Handled = true;

        _popup.PopupPredicted(Loc.GetString("rmc-xeno-egg-plant-self"), Loc.GetString("rmc-xeno-egg-plant", ("user", args.User)), egg, args.User);

        _doAfter.TryStartDoAfter(doAfter);
    }

    private void OnXenoEggDoAfter(Entity<XenoEggComponent> egg, ref PlaceEggDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled)
            return;

        if (egg.Comp.State != XenoEggState.Item ||
    !TryComp(egg, out TransformComponent? xform))
        {
            return;
        }

        bool handle = args.Handled;
        if (!CanPlaceEgg(egg, args.User, true, EntityManager.GetCoordinates(args.Coordinates), ref handle))
        {
            args.Handled = handle;
            return;
        }

        if (!_plasma.TryRemovePlasmaPopup(args.User, 30))
            return;

        args.Handled = true;

        // Hand code is god-awful and its reach distance is inconsistent with args.CanReach
        // so we need to set the position ourselves.
        _transform.SetCoordinates(egg, EntityManager.GetCoordinates(args.Coordinates));
        _transform.SetLocalRotation(egg, 0);

        SetEggState(egg, XenoEggState.Growing);
        _transform.AnchorEntity(egg, xform);
        _audio.PlayPredicted(egg.Comp.PlantSound, egg, args.User);
    }

    private bool CanPlaceEgg(Entity<XenoEggComponent> egg, EntityUid user, bool reachable, EntityCoordinates location, ref bool handled)
    {
        if (!reachable)
        {
            if (_timing.IsFirstTimePredicted)
                _popup.PopupCoordinates(Loc.GetString("cm-xeno-cant-reach-there"), location, Filter.Local(), true);

            return false;
        }

        if (HasComp<MarineComponent>(user))
        {
            // TODO RMC14 this should have a better filter than marine component
            if (!handled)
            {
                _hands.TryDrop(user, egg, location);
                _popup.PopupClient(Loc.GetString("cm-xeno-egg-failed-plant-outside"), user, user);
            }

            handled = true;
            return false;
        }

        if (_transform.GetGrid(location) is not { } gridId ||
            !TryComp(gridId, out MapGridComponent? grid))
        {
            return false;
        }

        var tile = _map.TileIndicesFor(gridId, grid, location);
        var anchored = _map.GetAnchoredEntitiesEnumerator(gridId, grid, tile);
        var hasWeeds = false;
        while (anchored.MoveNext(out var uid))
        {
            if (HasComp<XenoEggComponent>(uid))
            {
                var msg = Loc.GetString("cm-xeno-egg-failed-already-there");
                _popup.PopupClient(msg, uid.Value, user, PopupType.SmallCaution);
                handled = true;
                return false;
            }

            if (HasComp<XenoConstructComponent>(uid) ||
                _tags.HasAnyTag(uid.Value, StructureTag, AirlockTag) ||
                HasComp<StrapComponent>(uid))
            {
                var msg = Loc.GetString("cm-xeno-egg-blocked");
                _popup.PopupClient(msg, uid.Value, user, PopupType.SmallCaution);
                handled = true;
                return false;
            }

            if (HasComp<XenoWeedsComponent>(uid))
                hasWeeds = true;
        }

        if (_turf.IsTileBlocked(gridId, tile, Impassable | MidImpassable | HighImpassable, grid))
        {
            var msg = Loc.GetString("cm-xeno-egg-blocked");
            _popup.PopupClient(msg, location, user, PopupType.SmallCaution);
            handled = true;
            return false;
        }

        // TODO RMC14 only on hive weeds
        if (!hasWeeds)
        {
            _popup.PopupClient(Loc.GetString("cm-xeno-egg-failed-must-weeds"), user, user);
            return false;
        }

        if (!_plasma.HasPlasmaPopup(user, 30))
            return false;

        return true;
    }

    private void OnXenoEggActivateInWorld(Entity<XenoEggComponent> egg, ref ActivateInWorldEvent args)
    {
        // TODO RMC14 multiple hive support
        if (!HasComp<XenoParasiteComponent>(args.User) && (!HasComp<XenoComponent>(args.User) || !HasComp<HandsComponent>(args.User)))
            return;

        if (Open(egg, args.User, out _))
            args.Handled = true;
    }

    private void OnXenoEggInteractUsing(Entity<XenoEggComponent> egg, ref InteractUsingEvent args)
    {
        // Doesn't check hive or if a xeno is doing it
        if (!HasComp<XenoParasiteComponent>(args.Used) || !_rmcHands.IsPickupByAllowed(args.Used, args.User))
            return;

        args.Handled = true;

        if (_net.IsClient)
            return;

        if (_mobState.IsDead(args.Used))
        {
            _popup.PopupEntity(Loc.GetString("rmc-xeno-egg-dead-child"), args.User, args.User, PopupType.SmallCaution);
            return;
        }

        if (egg.Comp.State == XenoEggState.Growing || egg.Comp.State == XenoEggState.Grown)
        {
            _popup.PopupEntity(Loc.GetString("rmc-xeno-egg-has-child"), args.User, args.User, PopupType.SmallCaution);
            return;
        }
        else if (egg.Comp.State != XenoEggState.Opened)
        {
            _popup.PopupEntity(Loc.GetString("rmc-xeno-egg-fail-return"), args.User, args.User, PopupType.SmallCaution);
            return;
        }

        if (_mind.TryGetMind(args.Used, out _, out _))
        {
            _popup.PopupEntity(Loc.GetString("rmc-xeno-egg-awake-child", ("parasite", args.Used)), args.User, args.User, PopupType.SmallCaution);
            return;
        }

        _popup.PopupEntity(Loc.GetString("rmc-xeno-egg-return-user"), args.User, args.User);
        _popup.PopupEntity(Loc.GetString("rmc-xeno-egg-return", ("user", args.User), ("parasite", args.Used)), egg, Filter.PvsExcept(args.User), true);

        SetEggState(egg, XenoEggState.Grown);

        QueueDel(args.Used);

    }

    private void OnXenoEggStepTriggerAttempt(Entity<XenoEggComponent> egg, ref StepTriggerAttemptEvent args)
    {
        if (CanTrigger(args.Tripper))
            args.Continue = true;
    }

    private void OnXenoEggStepTriggered(Entity<XenoEggComponent> egg, ref StepTriggeredOffEvent args)
    {
        TryTrigger(egg, args.Tripper);
    }

    private bool CanTrigger(EntityUid user)
    {
        return HasComp<InfectableComponent>(user) &&
               !HasComp<VictimInfectedComponent>(user) &&
               !_mobState.IsDead(user);
    }

    public bool Open(Entity<XenoEggComponent> egg, EntityUid? user, out EntityUid? spawned)
    {
        spawned = null;
        if (egg.Comp.State == XenoEggState.Opened)
        {
            if (HasComp<XenoParasiteComponent>(user))
            {
                if (_mobState.IsDead(user.Value))
                    return true;

                SetEggState(egg, XenoEggState.Grown);

                if (_net.IsClient)
                    return true;

                _popup.PopupEntity(Loc.GetString("rmc-xeno-egg-return-self", ("parasite", user)), egg);

                QueueDel(user);

                return true;
            }
            else
            {
                if (user != null)
                    _popup.PopupClient(Loc.GetString("cm-xeno-egg-clear"), egg, user.Value);

                if (_net.IsClient)
                    return true;

                QueueDel(egg);

                return true;
            }
        }

        if (HasComp<XenoParasiteComponent>(user))
        {
            if (egg.Comp.State == XenoEggState.Grown || egg.Comp.State == XenoEggState.Growing)
                _popup.PopupClient(Loc.GetString("rmc-xeno-egg-has-child"), user.Value);
            return true;
        }

        if (egg.Comp.State != XenoEggState.Grown)
        {
            if (user != null)
                _popup.PopupClient(Loc.GetString("cm-xeno-egg-not-developed"), egg, user.Value);

            return false;
        }

        SetEggState(egg, XenoEggState.Opened);

        if (_net.IsClient)
            return true;

        if (TryComp(egg, out TransformComponent? xform))
        {
            spawned = SpawnAtPosition(egg.Comp.Spawn, xform.Coordinates);
            _xeno.SetHive(spawned.Value, egg.Comp.Hive);
            if (spawned != null && TryComp<ParasiteAIComponent>(spawned, out var ai))
                _parasite.GoIdle((spawned.Value, ai));
        }

        return true;
    }

    private void SetEggState(Entity<XenoEggComponent> egg, XenoEggState state)
    {
        egg.Comp.State = state;
        Dirty(egg);

        if (state == XenoEggState.Opened)
            RemCompDeferred<XenoFriendlyComponent>(egg);

        var ev = new XenoEggStateChangedEvent();
        RaiseLocalEvent(egg, ref ev);
    }

    private void AttachOvipositor(Entity<XenoAttachedOvipositorComponent?> xeno)
    {
        if (EnsureComp<XenoAttachedOvipositorComponent>(xeno, out var attached))
            return;

        xeno.Comp = attached;
        foreach (var (actionId, _) in _actions.GetActions(xeno))
        {
            if (TryComp(actionId, out XenoGrowOvipositorActionComponent? action))
            {
                _actions.SetCooldown(actionId, action.AttachCooldown);
                _actions.SetToggled(actionId, true);
            }
        }
    }

    private void DetachOvipositor(Entity<XenoAttachedOvipositorComponent> xeno)
    {
        if (!RemCompDeferred<XenoAttachedOvipositorComponent>(xeno))
            return;

        foreach (var (actionId, _) in _actions.GetActions(xeno))
        {
            if (TryComp(actionId, out XenoGrowOvipositorActionComponent? action))
            {
                _actions.SetCooldown(actionId, action.DetachCooldown);
                _actions.SetToggled(actionId, false);
            }
        }

        _popup.PopupClient(Loc.GetString("cm-xeno-ovipositor-detach"), xeno, xeno);
    }

    private bool TryTrigger(Entity<XenoEggComponent> egg, EntityUid tripper)
    {
        if (egg.Comp.State != XenoEggState.Grown ||
            !CanTrigger(tripper))
        {
            return false;
        }

        if (!_interaction.InRangeUnobstructed(egg.Owner, tripper) ||
            !Open(egg, tripper, out var spawned) ||
            !TryComp(spawned, out XenoParasiteComponent? parasite))
        {
            return false;
        }

        _parasite.Infect((spawned.Value, parasite), tripper, force: true);
        _stun.TryParalyze(tripper, egg.Comp.KnockdownTime, true);
        return true;
    }

    public override void Update(float frameTime)
    {
        if (_net.IsClient)
            return;

        var time = _timing.CurTime;
        var oviQuery = EntityQueryEnumerator<XenoOvipositorCapableComponent, XenoAttachedOvipositorComponent, TransformComponent>();
        while (oviQuery.MoveNext(out var uid, out var capable, out var attached, out var xform))
        {
            if (attached.NextEgg == null)
            {
                attached.NextEgg = time + capable.Cooldown;
                continue;
            }

            if (time < attached.NextEgg)
                continue;

            if (TryComp(uid, out MobStateComponent? state) &&
                _mobState.IsIncapacitated(uid, state))
            {
                continue;
            }

            attached.NextEgg = time + capable.Cooldown;
            Dirty(uid, attached);

            var egg = SpawnAtPosition(capable.Spawn, xform.Coordinates.Offset(capable.Offset));
            if (TryComp(egg, out XenoEggComponent? eggComp) &&
                TryComp(uid, out XenoComponent? xeno))
            {
                eggComp.Hive = xeno.Hive;
                Dirty(egg, eggComp);
            }

            _transform.SetLocalRotation(egg, Angle.Zero);
        }

        var eggQuery = EntityQueryEnumerator<XenoEggComponent, TransformComponent>();
        while (eggQuery.MoveNext(out var uid, out var egg, out var xform))
        {
            if (egg.State == XenoEggState.Grown &&
                _stepTriggerQuery.TryComp(uid, out var stepTrigger) &&
                stepTrigger.CurrentlySteppedOn.Count > 0)
            {
                foreach (var current in stepTrigger.CurrentlySteppedOn)
                {
                    if (TryTrigger((uid, egg), current))
                        break;
                }
            }

            if (!xform.Anchored ||
                egg.State != XenoEggState.Growing)
            {
                continue;
            }

            egg.GrowAt ??= time + _random.Next(egg.MinTime, egg.MaxTime);

            if (time < egg.GrowAt || egg.State != XenoEggState.Growing)
                continue;

            SetEggState((uid, egg), XenoEggState.Grown);
        }
    }

    private void OnGetVerbs(Entity<XenoEggComponent> ent, ref GetVerbsEvent<ActivationVerb> args)
    {
        var uid = args.User;

        // if it doesn't have an actor and we can't reach it then don't add the verb
        if (!TryComp(uid, out ActorComponent? actor))
            return;

        if (!TryComp(uid, out GhostComponent? ghostComp))
            return;

        if (ent.Comp.State == XenoEggState.Opened || ent.Comp.State == XenoEggState.Growing)
            return;

        var parasiteVerb = new ActivationVerb
        {
            Text = Loc.GetString("rmc-xeno-egg-ghost-verb"),
            Act = () =>
            {
                _ui.TryOpenUi(ent.Owner, XenoEggGhostUI.Key, uid);
            },

            Impact = LogImpact.High,
        };

        args.Verbs.Add(parasiteVerb);
    }
}

[Serializable, NetSerializable]
public enum XenoEggGhostUI
{
    Key
}

[Serializable, NetSerializable]
public sealed class XenoEggGhostBuiMsg() : BoundUserInterfaceMessage
{

}