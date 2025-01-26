using Content.Shared._RMC14.Hands;
using Content.Shared._RMC14.Xenonids.Construction.EggMorpher;
using Content.Shared._RMC14.Xenonids.Egg;
using Content.Shared._RMC14.Xenonids.Hive;
using Content.Shared._RMC14.Xenonids.Parasite;
using Content.Shared._RMC14.Xenonids.Projectile.Parasite;
using Content.Shared.Coordinates;
using Content.Shared.Database;
using Content.Shared.Ghost;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction;
using Content.Shared.Mobs.Systems;
using Content.Shared.Popups;
using Content.Shared.StepTrigger.Systems;
using Content.Shared.Stunnable;
using Content.Shared.Verbs;
using Robust.Shared.Network;
using Robust.Shared.Player;
using Robust.Shared.Timing;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace Content.Server._RMC14.Xenonids.Construction.EggMorpher;

public sealed partial class EggMorpherSystem : SharedEggMorpherSystem
{
    [Dependency] private readonly IGameTiming _time = default!;
    [Dependency] private readonly SharedXenoHiveSystem _hive = default!;
    [Dependency] private readonly SharedUserInterfaceSystem _ui = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly SharedInteractionSystem _interaction = default!;
    [Dependency] private readonly SharedXenoParasiteSystem _parasite = default!;
    [Dependency] private readonly SharedStunSystem _stun = default!;
    [Dependency] private readonly SharedHandsSystem _hand = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly INetManager _net = default!;
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<EggMorpherComponent, InteractHandEvent>(OnInteractHand);
        SubscribeLocalEvent<EggMorpherComponent, InteractUsingEvent>(OnInteractUsing);


        SubscribeLocalEvent<EggMorpherComponent, XenoChangeParasiteReserveMessage>(OnChangeParasiteReserve);
        SubscribeLocalEvent<EggMorpherComponent, GetVerbsEvent<ActivationVerb>>(OnGetVerbs);

        SubscribeLocalEvent<EggMorpherComponent, StepTriggerAttemptEvent>(OnEggMorpherStepAttempt);
        SubscribeLocalEvent<EggMorpherComponent, StepTriggeredOffEvent>(OnEggMorpherStepTriggered);

    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var curTime = _time.CurTime;
        var eggMorpherQuery = EntityQueryEnumerator<EggMorpherComponent>();
        while (eggMorpherQuery.MoveNext(out var eggMorpherEnt, out var eggMorpherComp))
        {
            if (eggMorpherComp.GrowMaxParasites <= eggMorpherComp.CurParasites)
            {
                continue;
            }

            var newSpawnTime = GetParasiteSpawnCooldown((eggMorpherEnt, eggMorpherComp)) + curTime;

            if (eggMorpherComp.NextSpawnAt < curTime)
            {
                eggMorpherComp.CurParasites++;
                eggMorpherComp.NextSpawnAt = newSpawnTime;
                Dirty(eggMorpherEnt, eggMorpherComp);
                continue;
            }

            if (newSpawnTime < eggMorpherComp.NextSpawnAt || eggMorpherComp.NextSpawnAt is null)
            {
                eggMorpherComp.NextSpawnAt = newSpawnTime;
            }
        }
    }

    private void OnInteractHand(Entity<EggMorpherComponent> eggMorpher, ref InteractHandEvent args)
    {
        var (ent, comp) = eggMorpher;
        var user = args.User;

        if (HasComp<XenoParasiteComponent>(user))
        {
            args.Handled = true;

            if (comp.MaxParasites <= comp.CurParasites)
            {
                _popup.PopupEntity(Loc.GetString("rmc-xeno-construction-egg-morpher-already-full"), eggMorpher, user);
                return;
            }

            if (_mobState.IsDead(user))
                return;

            if (_net.IsClient)
                return;

            _popup.PopupEntity(Loc.GetString("rmc-xeno-egg-return-self", ("parasite", user)), eggMorpher);

            QueueDel(user);
            comp.CurParasites++;

            return;
        }

        if (!TryCreateParasiteFromEggMorpher(eggMorpher, out var newParasite))
        {
            _popup.PopupEntity(Loc.GetString("rmc-xeno-construction-egg-morpher-no-parasites"), eggMorpher, user);
            return;
        }
        args.Handled = true;
    }
    private void OnInteractUsing(Entity<EggMorpherComponent> eggMorpher, ref InteractUsingEvent args)
    {
        var (ent, comp) = eggMorpher;

        var user = args.User;
        var used = args.Used;

        if (!HasComp<XenoParasiteComponent>(used))
        {
            _popup.PopupEntity(Loc.GetString("rmc-xeno-construction-egg-morpher-attempt-insert-non-parasite"), eggMorpher, user);
            return;
        }

        if (!_mobState.IsAlive(used))
        {
            _popup.PopupEntity(Loc.GetString("rmc-xeno-egg-dead-child"), eggMorpher, user);
            return;
        }

        if (comp.MaxParasites <= comp.CurParasites)
        {
            _popup.PopupEntity(Loc.GetString("rmc-xeno-construction-egg-morpher-already-full"), eggMorpher, user);
            return;
        }

        args.Handled = true;
        QueueDel(used);
        comp.CurParasites++;
    }


    private void OnChangeParasiteReserve(Entity<EggMorpherComponent> eggMorpher, ref XenoChangeParasiteReserveMessage args)
    {
        eggMorpher.Comp.ReservedParasites = args.NewReserve;
    }

    private void OnGetVerbs(Entity<EggMorpherComponent> eggMorpher, ref GetVerbsEvent<ActivationVerb> args)
    {
        var (ent, comp) = eggMorpher;
        var user = args.User;
        if (_hive.FromSameHive(user, ent))
        {
            var changeReserveVerb = new ActivationVerb()
            {
                Text = Loc.GetString("xeno-reserve-parasites-verb"),
                Act = () =>
                {
                    _ui.OpenUi(ent, XenoReserveParasiteChangeUI.Key, user);
                }
            };

            args.Verbs.Add(changeReserveVerb);
        }

        if (HasComp<ActorComponent>(user) && HasComp<GhostComponent>(user) &&
            comp.CurParasites > comp.ReservedParasites && comp.CurParasites > 0)
        {
            var parasiteVerb = new ActivationVerb
            {
                Text = Loc.GetString("rmc-xeno-egg-ghost-verb"),
                Act = () =>
                {
                    _ui.TryOpenUi(ent, XenoParasiteGhostUI.Key, user);
                },

                Impact = LogImpact.High,
            };

            args.Verbs.Add(parasiteVerb);
        }
    }

    public bool TryCreateParasiteFromEggMorpher(Entity<EggMorpherComponent> eggMorpher, [NotNullWhen(true)] out EntityUid? parasite)
    {
        parasite = null;

        var (ent, comp) = eggMorpher;
        if (comp.CurParasites <= comp.ReservedParasites || comp.CurParasites <= 0)
        {
            return false;
        }
        comp.CurParasites--;

        parasite = SpawnAtPosition(EggMorpherComponent.ParasitePrototype, ent.ToCoordinates());
        Dirty(eggMorpher);
        return true;
    }

    private void OnEggMorpherStepAttempt(Entity<EggMorpherComponent> eggMorpher, ref StepTriggerAttemptEvent args)
    {
        if (CanTrigger(args.Tripper))
            args.Continue = true;
    }

    private void OnEggMorpherStepTriggered(Entity<EggMorpherComponent> eggMorpher, ref StepTriggeredOffEvent args)
    {
        TryTrigger(eggMorpher, args.Tripper);
    }

    private bool CanTrigger(EntityUid user)
    {
        return TryComp<InfectableComponent>(user, out var infected)
               && !infected.BeingInfected
               && !_mobState.IsDead(user)
               && !HasComp<VictimInfectedComponent>(user);
    }

    private bool TryTrigger(Entity<EggMorpherComponent> eggMorpher, EntityUid tripper)
    {
        if (!CanTrigger(tripper))
        {
            return false;
        }

        if (!_interaction.InRangeUnobstructed(eggMorpher.Owner, tripper))
        {
            return false;
        }

        if (!TryCreateParasiteFromEggMorpher(eggMorpher, out var spawnedParasite))
        {
            return false;
        }

        var parasiteComp = EnsureComp<XenoParasiteComponent>(spawnedParasite.Value);
        _parasite.Infect((spawnedParasite.Value, parasiteComp), tripper, force: true);

        return true;
    }

    private TimeSpan GetParasiteSpawnCooldown(Entity<EggMorpherComponent> eggMorpher)
    {
        if (_hive.GetHive(eggMorpher.Owner) is not Entity<HiveComponent> hive)
        {
            return eggMorpher.Comp.StandardSpawnCooldown;
        }

        if (hive.Comp.CurrentQueen is EntityUid curQueen &&
            HasComp<XenoAttachedOvipositorComponent>(curQueen))
        {
            return eggMorpher.Comp.OviSpawnCooldown;
        }

        return eggMorpher.Comp.StandardSpawnCooldown;
    }
}
