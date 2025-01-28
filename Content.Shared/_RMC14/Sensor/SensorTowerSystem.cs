using Content.Shared._RMC14.Marines.Skills;
using Content.Shared._RMC14.TacticalMap;
using Content.Shared._RMC14.Xenonids;
using Content.Shared.DoAfter;
using Content.Shared.Examine;
using Content.Shared.Interaction;
using Content.Shared.Popups;
using Content.Shared.Tools.Systems;
using Robust.Shared.Network;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Shared._RMC14.Sensor;

public sealed class SensorTowerSystem : EntitySystem
{
    [Dependency] private readonly SharedAppearanceSystem _appearance = default!;
    [Dependency] private readonly SharedDoAfterSystem _doAfter = default!;
    [Dependency] private readonly INetManager _net = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly SkillsSystem _skills = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly SharedToolSystem _tool = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<TacticalMapIncludeXenosEvent>(OnTacticalMapIncludeXenos);

        SubscribeLocalEvent<SensorTowerComponent, MapInitEvent>(OnSensorTowerMapInit);
        SubscribeLocalEvent<SensorTowerComponent, InteractUsingEvent>(OnSensorTowerInteractUsing);
        SubscribeLocalEvent<SensorTowerComponent, InteractHandEvent>(OnSensorTowerInteractHand);
        SubscribeLocalEvent<SensorTowerComponent, ExaminedEvent>(OnSensorTowerExamined);
        SubscribeLocalEvent<SensorTowerComponent, SensorTowerRepairDoAfterEvent>(OnSensorTowerRepairDoAfter);
        SubscribeLocalEvent<SensorTowerComponent, SensorTowerDestroyDoAfterEvent>(OnSensorTowerDestroyDoAfter);
    }

    private void OnTacticalMapIncludeXenos(ref TacticalMapIncludeXenosEvent ev)
    {
        var towers = EntityQueryEnumerator<SensorTowerComponent>();
        while (towers.MoveNext(out var tower))
        {
            if (tower.State == SensorTowerState.On)
            {
                ev.Include = true;
                return;
            }
        }
    }

    private void OnSensorTowerMapInit(Entity<SensorTowerComponent> ent, ref MapInitEvent args)
    {
        UpdateAppearance(ent);
    }

    private void OnSensorTowerInteractUsing(Entity<SensorTowerComponent> ent, ref InteractUsingEvent args)
    {
        var user = args.User;
        if (!_skills.HasSkill(user, ent.Comp.Skill, ent.Comp.SkillLevel))
        {
            var msg = Loc.GetString("rmc-skills-no-training", ("target", ent));
            _popup.PopupClient(msg, ent, user, PopupType.SmallCaution);
            return;
        }

        var used = args.Used;

        var correctQuality = ent.Comp.State switch
        {
            SensorTowerState.Weld => ent.Comp.WeldingQuality,
            SensorTowerState.Wire => ent.Comp.CuttingQuality,
            SensorTowerState.Wrench => ent.Comp.WrenchQuality,
            _ => throw new ArgumentOutOfRangeException(),
        };

        args.Handled = true;

        if (_tool.HasQuality(used, correctQuality))
            TryRepair(ent, user, used, ent.Comp.State);
    }

    private void OnSensorTowerInteractHand(Entity<SensorTowerComponent> ent, ref InteractHandEvent args)
    {
        var user = args.User;
        if (HasComp<XenoComponent>(user))
        {
            Destroy(ent, user);
            return;
        }

        if (!_skills.HasSkill(user, ent.Comp.Skill, ent.Comp.SkillLevel))
        {
            _popup.PopupClient("You have no clue how this thing works...", ent, user, PopupType.SmallCaution);
            return;
        }

        ref var state = ref ent.Comp.State;
        var popup = state switch
        {
            SensorTowerState.Weld => "Use a blowtorch, then wirecutters, then wrench to repair it.",
            SensorTowerState.Wire => "Use some wirecutters, then wrench to repair it.",
            SensorTowerState.Wrench => "Use a wrench to repair it.",
            SensorTowerState.Off => $"The {Name(ent)} lights up.",
            SensorTowerState.On => $"The {Name(ent)} goes dark.",
            _ => throw new ArgumentOutOfRangeException(),
        };
        _popup.PopupClient(popup, ent, user, PopupType.Medium);

        if (state < SensorTowerState.Off)
            return;

        if (state == SensorTowerState.Off)
            state = SensorTowerState.On;
        else if (state == SensorTowerState.On)
            state = SensorTowerState.Off;

        Dirty(ent);
        UpdateAppearance(ent);
    }

    private void OnSensorTowerExamined(Entity<SensorTowerComponent> ent, ref ExaminedEvent args)
    {
        if (HasComp<XenoComponent>(args.Examiner))
            return;

        using (args.PushGroup(nameof(SensorTowerComponent)))
        {
            // TODO: localize
            var text = ent.Comp.State switch
            {
                SensorTowerState.Weld => "This one is heavily damaged. Use a blowtorch, wirecutters, then a wrench to repair it.",
                SensorTowerState.Wire => "This one is heavily damaged. Use wirecutters, then a wrench to repair it.",
                SensorTowerState.Wrench => "This one is heavily damaged. Use a wrench to repair it.",
                SensorTowerState.Off => "It looks like it is offline.",
                SensorTowerState.On => "It looks like it is online.",
                _ => throw new ArgumentOutOfRangeException(),
            };
            args.PushText(text);

            if (ent.Comp.State < SensorTowerState.Off)
            {
                var tool = ent.Comp.State switch
                {
                    SensorTowerState.Wrench => "a [color=cyan]Wrench[/color]",
                    SensorTowerState.Wire => "[color=cyan]Wirecutters[/color]",
                    SensorTowerState.Weld => "a [color=cyan]Welder[/color]",
                    _ => throw new ArgumentOutOfRangeException(),
                };

                args.PushMarkup($"Use {tool} to repair it!");
            }
        }
    }

    private void OnSensorTowerRepairDoAfter(Entity<SensorTowerComponent> ent, ref SensorTowerRepairDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled)
            return;

        args.Handled = true;

        if (ent.Comp.State != args.State)
            return;

        ent.Comp.State = args.State switch
        {
            SensorTowerState.Weld => SensorTowerState.Wire,
            SensorTowerState.Wire => SensorTowerState.Wrench,
            SensorTowerState.Wrench => SensorTowerState.Off,
            _ => throw new ArgumentOutOfRangeException(),
        };

        Dirty(ent);
        UpdateAppearance(ent);
    }

    private void OnSensorTowerDestroyDoAfter(Entity<SensorTowerComponent> ent, ref SensorTowerDestroyDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled)
            return;

        args.Handled = true;

        ent.Comp.State = SensorTowerState.Weld;
        Dirty(ent);
        UpdateAppearance(ent);
    }

    private void TryRepair(Entity<SensorTowerComponent> tower, EntityUid user, EntityUid used, SensorTowerState state)
    {
        var quality = state switch
        {
            SensorTowerState.Weld => tower.Comp.WeldingQuality,
            SensorTowerState.Wire => tower.Comp.CuttingQuality,
            SensorTowerState.Wrench => tower.Comp.WrenchQuality,
            _ => throw new ArgumentOutOfRangeException(nameof(state), state, null),
        };

        var delay = state switch
        {
            SensorTowerState.Weld => tower.Comp.WeldingDelay,
            SensorTowerState.Wire => tower.Comp.CuttingDelay,
            SensorTowerState.Wrench => tower.Comp.WrenchDelay,
            _ => throw new ArgumentOutOfRangeException(nameof(state), state, null)
        };

        delay *= _skills.GetSkillDelayMultiplier(user, tower.Comp.Skill);
        _tool.UseTool(
            used,
            user,
            tower,
            (float)delay.TotalSeconds,
            quality,
            new SensorTowerRepairDoAfterEvent(state),
            tower.Comp.WeldingCost
        );
    }

    private void UpdateAppearance(Entity<SensorTowerComponent> tower)
    {
        _appearance.SetData(tower, SensorTowerLayers.Layer, tower.Comp.State);
    }

    private void Destroy(Entity<SensorTowerComponent> tower, EntityUid user)
    {
        if (tower.Comp.State == SensorTowerState.Weld)
        {
            _popup.PopupClient("We stare at the experimental sensor tower cluelessly.", user, user, PopupType.SmallCaution);
            return;
        }

        var ev = new SensorTowerDestroyDoAfterEvent();
        var doAfter = new DoAfterArgs(EntityManager, user, tower.Comp.DestroyDelay, ev, tower, tower, user);
        if (_doAfter.TryStartDoAfter(doAfter))
        {
            _popup.PopupClient($"You start wrenching apart the {Name(tower)}'s panels and reaching inside it!", tower, user, PopupType.Medium);
        }
    }

    public override void Update(float frameTime)
    {
        if (_net.IsClient)
            return;

        var time = _timing.CurTime;
        var query = EntityQueryEnumerator<SensorTowerComponent>();
        while (query.MoveNext(out var uid, out var tower))
        {
            if (tower.State != SensorTowerState.On)
                continue;

            if (time < tower.NextBreakAt)
                continue;

            if (!_random.Prob(tower.BreakChance))
            {
                tower.NextBreakAt = time + tower.BreakEvery;
                continue;
            }

            if (_random.Prob(0.75f))
            {
                _popup.PopupEntity($"The {Name(uid)} beeps wildly and sprays random pieces everywhere! Use a wrench to repair it.", uid, uid, PopupType.LargeCaution);
                tower.State = SensorTowerState.Wrench;
            }
            else
            {
                _popup.PopupEntity($"The {Name(uid)} beeps wildly and a fuse blows! Use wirecutters, then a wrench to repair it.", uid, uid, PopupType.LargeCaution);
                tower.State = SensorTowerState.Wire;
            }

            UpdateAppearance((uid, tower));
        }
    }
}
