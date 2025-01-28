using Content.Shared.Players;
using Robust.Shared.Player;

namespace Content.Shared._RMC14.GhostAppearance;

public sealed class SharedDeadGhostVisualsSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<RMCGhostAppearanceComponent, PlayerAttachedEvent>(OnPlayerAttached);
    }

    private void OnPlayerAttached(Entity<RMCGhostAppearanceComponent> ent, ref PlayerAttachedEvent args)
    {
        if (args.Player.GetMind() is { } mind)
        {
            ent.Comp.MindId = mind;
            Dirty(ent);
        }
    }
}
