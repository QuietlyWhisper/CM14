﻿using Content.Shared._RMC14.NPC;
using Content.Shared.Examine;
using Content.Shared.Interaction.Events;
using Content.Shared.Actions;
using Content.Shared._RMC14.Xenonids.Rest;
using Content.Shared._RMC14.Xenonids.Pheromones;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Robust.Shared.Containers;
using Robust.Shared.Player;
using Content.Shared._RMC14.Xenonids.Egg;
using System.Reflection.Metadata.Ecma335;

namespace Content.Shared._RMC14.Xenonids.Parasite;

public abstract partial class SharedXenoParasiteSystem
{
    [Dependency] private readonly SharedRMCNPCSystem _rmcNpc = default!;
    [Dependency] private readonly SharedActionsSystem _actions = default!;
    [Dependency] private readonly EntityLookupSystem _entityLookup = default!;

    public void IntializeAI()
    {
        SubscribeLocalEvent<XenoParasiteComponent, PlayerAttachedEvent>(OnPlayerAdded);
        SubscribeLocalEvent<XenoParasiteComponent, PlayerDetachedEvent>(OnPlayerRemoved);

        SubscribeLocalEvent<ParasiteAIComponent, ComponentStartup>(OnAIAdded);
        SubscribeLocalEvent<ParasiteAIComponent, ExaminedEvent>(OnAIExamined);
        SubscribeLocalEvent<ParasiteAIComponent, DroppedEvent>(OnAIDropPickup);
        SubscribeLocalEvent<ParasiteAIComponent, EntGotInsertedIntoContainerMessage>(OnAIDropPickup);

        SubscribeLocalEvent<ParasiteTiredOutComponent, MapInitEvent>(OnParasiteAIMapInit);
        SubscribeLocalEvent<ParasiteTiredOutComponent, UpdateMobStateEvent>(OnParasiteAIUpdateMobState,
            after: [typeof(MobThresholdSystem), typeof(SharedXenoPheromonesSystem)]);
    }

    private void OnPlayerAdded(Entity<XenoParasiteComponent> para, ref PlayerAttachedEvent args)
    {
        RemCompDeferred<ParasiteAIComponent>(para);
    }

    private void OnPlayerRemoved(Entity<XenoParasiteComponent> para, ref PlayerDetachedEvent args)
    {
        EnsureComp<ParasiteAIComponent>(para);
    }

    private void OnAIAdded(Entity<ParasiteAIComponent> para, ref ComponentStartup args)
    {
        HandleDeathTimer(para);
        _rmcNpc.WakeNPC(para);
    }

    private void OnAIExamined(Entity<ParasiteAIComponent> para, ref ExaminedEvent args)
    {
        if (_mobState.IsDead(para) || !HasComp<XenoComponent>(args.Examiner))
            return;

        switch (para.Comp.Mode)
        {
            case ParasiteMode.Idle:
                args.PushMarkup($"{Loc.GetString("rmc-xeno-parasite-ai-idle", ("parasite", para))}");
                break;
            case ParasiteMode.Active:
                args.PushMarkup($"{Loc.GetString("rmc-xeno-parasite-ai-active", ("parasite", para))}");
                break;
            case ParasiteMode.Dying:
                args.PushMarkup($"{Loc.GetString("rmc-xeno-parasite-ai-dying", ("parasite", para))}");
                break;
        }
    }


    private void OnAIDropPickup<T>(Entity<ParasiteAIComponent> para, ref T args) where T : EntityEventArgs
    {
        HandleDeathTimer(para);
        GoIdle(para);
    }

    public void HandleDeathTimer(Entity<ParasiteAIComponent> para)
    {
        if (_container.TryGetContainingContainer((para, null, null), out var carry) && HasComp<XenoComponent>(carry.Owner)) // TODO Check for parasite thrower
        {
            para.Comp.DeathTime = null;
            if (para.Comp.Mode == ParasiteMode.Dying)
            {
                para.Comp.Mode = ParasiteMode.Active;
                GoIdle(para);
            }
            return;
        }

        if (para.Comp.DeathTime == null)
            para.Comp.DeathTime = _timing.CurTime + para.Comp.LifeTime;
    }

    public void UpdateAI(Entity<ParasiteAIComponent> para, TimeSpan currentTime)
    {
        CheckCannibalize(para);
        if (para.Comp.DeathTime != null && currentTime > para.Comp.DeathTime || para.Comp.JumpsLeft <= 0)
        {
            if (para.Comp.Mode != ParasiteMode.Dying)
            {
                para.Comp.Mode = ParasiteMode.Dying;

                if (HasComp<XenoRestingComponent>(para))
                    DoRestAction(para);

                ChangeHTN(para, ParasiteMode.Dying);
                _rmcNpc.WakeNPC(para);

                Dirty(para);
            }

            CheckDeath(para);
            return;
        }

        if (para.Comp.Mode == ParasiteMode.Idle && currentTime > para.Comp.NextActiveTime)
            GoActive(para);
    }

    public void GoIdle(Entity<ParasiteAIComponent> para)
    {
        if (para.Comp.Mode != ParasiteMode.Active)
            return;

        if (!HasComp<XenoRestingComponent>(para))
            DoRestAction(para);

        _rmcNpc.SleepNPC(para);
        para.Comp.JumpsLeft = para.Comp.InitialJumps;
        para.Comp.Mode = ParasiteMode.Idle;

        para.Comp.NextActiveTime = _timing.CurTime + TimeSpan.FromSeconds(_random.Next(para.Comp.MinIdleTime, para.Comp.MaxIdleTime + 1));

        Dirty(para);
    }

    public void GoActive(Entity<ParasiteAIComponent> para)
    {
        if (para.Comp.Mode == ParasiteMode.Dying)
            return;

        if (HasComp<XenoRestingComponent>(para))
            DoRestAction(para);

        _rmcNpc.WakeNPC(para);
        ChangeHTN(para, ParasiteMode.Active);
        para.Comp.Mode = ParasiteMode.Active;
        Dirty(para);
    }

    private void DoRestAction(Entity<ParasiteAIComponent> para)
    {
        if (!TryComp<XenoComponent>(para, out var xeno))
            return;

        if (!TryComp<InstantActionComponent>(xeno.Actions[para.Comp.RestAction], out var instant))
            return;


        _actions.PerformAction(para, null, xeno.Actions[para.Comp.RestAction], instant, instant.Event, _timing.CurTime);
    }

    protected virtual void ChangeHTN(EntityUid parasite, ParasiteMode mode)
    {
    }

    private void CheckCannibalize(Entity<ParasiteAIComponent> para)
    {
        int totalParasites = 0;
        foreach (var parasite in _entityLookup.GetEntitiesInRange<ParasiteAIComponent>(_transform.GetMapCoordinates(para), para.Comp.RangeCheck))
        {
            if (parasite == para)
                continue;

            // Ignore those that are dead, not active, or already are being deleted
            if (TerminatingOrDeleted(parasite) || _mobState.IsDead(parasite) ||
                parasite.Comp.Mode != ParasiteMode.Active || _container.IsEntityInContainer(parasite))
                continue;

            totalParasites++;
        }

        if (totalParasites <= para.Comp.MaxSurroundingParas)
            return;

        // Get Eaten
        _popup.PopupCoordinates(Loc.GetString("rmc-xeno-parasite-ai-eaten", ("parasite", para)), _transform.GetMoverCoordinates(para), Popups.PopupType.SmallCaution);
        QueueDel(para);
    }

    private void CheckDeath(Entity<ParasiteAIComponent> para)
    {
        foreach (var egg in _entityLookup.GetEntitiesInRange<XenoEggComponent>(_transform.GetMapCoordinates(para), para.Comp.RangeCheck))
        {
            if (egg.Comp.State == XenoEggState.Opened)
                return;
        }

        EnsureComp<ParasiteTiredOutComponent>(para);

        // TODO RMC14 empty resin holes and eggmorpher
    }

    private void OnParasiteAIMapInit(Entity<ParasiteTiredOutComponent> dead, ref MapInitEvent args)
    {
        if (TryComp(dead, out MobStateComponent? mobState))
            _mobState.UpdateMobState(dead, mobState);
    }

    private void OnParasiteAIUpdateMobState(Entity<ParasiteTiredOutComponent> dead, ref UpdateMobStateEvent args)
    {
        args.State = MobState.Dead;
    }
}
