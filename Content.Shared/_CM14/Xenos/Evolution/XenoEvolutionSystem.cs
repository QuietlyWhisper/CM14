﻿using Content.Shared.Actions;
using Content.Shared.Mind;
using Robust.Shared.Network;
using Robust.Shared.Player;
using Robust.Shared.Timing;

namespace Content.Shared._CM14.Xenos.Evolution;

public sealed class XenoEvolutionSystem : EntitySystem
{
    [Dependency] private readonly SharedActionsSystem _action = default!;
    [Dependency] private readonly SharedMindSystem _mind = default!;
    [Dependency] private readonly INetManager _net = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly SharedUserInterfaceSystem _ui = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<XenoEvolveActionComponent, MapInitEvent>(OnXenoEvolveActionMapInit);
        SubscribeLocalEvent<XenoComponent, XenoOpenEvolutionsActionEvent>(OnXenoEvolveAction);
        SubscribeLocalEvent<XenoComponent, XenoEvolveBuiMessage>(OnXenoEvolveBui);
    }

    private void OnXenoEvolveActionMapInit(Entity<XenoEvolveActionComponent> ent, ref MapInitEvent args)
    {
        if (_action.TryGetActionData(ent, out _, false))
            _action.SetCooldown(ent, _timing.CurTime, _timing.CurTime + ent.Comp.Cooldown);
    }

    private void OnXenoEvolveAction(Entity<XenoComponent> xeno, ref XenoOpenEvolutionsActionEvent args)
    {
        if (_net.IsClient || !TryComp(xeno, out ActorComponent? actor))
            return;

        _ui.TryOpen(xeno.Owner, XenoEvolutionUIKey.Key, actor.PlayerSession);
    }

    private void OnXenoEvolveBui(Entity<XenoComponent> xeno, ref XenoEvolveBuiMessage args)
    {
        if (!_mind.TryGetMind(xeno, out var mindId, out _))
            return;

        var choices = xeno.Comp.EvolvesTo.Count;
        if (args.Choice >= choices || args.Choice < 0)
        {
            Log.Warning($"User {args.Session.Name} sent an out of bounds evolution choice: {args.Choice}. Choices: {choices}");
            return;
        }

        if (_net.IsClient)
            return;

        var evolution = Spawn(xeno.Comp.EvolvesTo[args.Choice], _transform.GetMoverCoordinates(xeno.Owner));
        _mind.TransferTo(mindId, evolution);
        _mind.UnVisit(mindId);
        Del(xeno.Owner);

        if (TryComp(xeno, out ActorComponent? actor))
            _ui.TryClose(xeno.Owner, XenoEvolutionUIKey.Key, actor.PlayerSession);
    }
}
