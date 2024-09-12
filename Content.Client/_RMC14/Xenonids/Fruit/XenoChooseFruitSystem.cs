﻿using Content.Shared._RMC14.Xenonids.Fruit;
using Content.Shared._RMC14.Xenonids.Fruit.Components;
using Robust.Shared.Timing;

namespace Content.Client._RMC14.Xenonids.Fruit;

public sealed class XenoChooseFruitSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<XenoFruitPlanterComponent, AfterAutoHandleStateEvent>(OnXenoConstructionAfterState);
    }

    private void OnXenoConstructionAfterState(Entity<XenoFruitPlanterComponent> ent, ref AfterAutoHandleStateEvent args)
    {
        if (!_timing.IsFirstTimePredicted)
            return;

        if (!TryComp(ent, out UserInterfaceComponent? ui))
            return;

        foreach (var bui in ui.ClientOpenInterfaces.Values)
        {
            if (bui is XenoChooseFruitBui chooseUi)
                chooseUi.Refresh();
        }
    }
}
