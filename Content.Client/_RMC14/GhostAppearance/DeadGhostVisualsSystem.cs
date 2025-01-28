using Content.Shared._RMC14.CCVar;
using Content.Shared._RMC14.GhostAppearance;
using Content.Shared._RMC14.Xenonids;
using Content.Shared._RMC14.Xenonids.Damage;
using Content.Shared.Ghost;
using Content.Shared.Mind;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Shared.Configuration;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using DrawDepth = Content.Shared.DrawDepth.DrawDepth;

namespace Content.Client._RMC14.GhostAppearance;

public sealed class DeadGhostVisualsSystem : EntitySystem
{
    [Dependency] private readonly IPrototypeManager _prototypes = default!;
    [Dependency] private readonly INetConfigurationManager _netConfigManager = default!;

    private readonly float _opacity = 0.5f;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<GhostComponent, PlayerAttachedEvent>(OnPlayerAttached);
    }

    private void OnPlayerAttached(Entity<GhostComponent> ent, ref PlayerAttachedEvent args)
    {
        UpdateGhostSprites();
    }

    private void UpdateGhostSprites()
    {
        var entities = EntityQueryEnumerator<RMCGhostAppearanceComponent, SpriteComponent, ActorComponent>();
        while (entities.MoveNext(out var ghostAppearance, out var sprite, out var actor))
        {
            if (!_netConfigManager.GetClientCVar(actor.PlayerSession.Channel, RMCCVars.RMCGhostAppearanceFromDeadCharacter))
                continue;

            if (!TryComp<MindComponent>(ghostAppearance.MindId, out var mind))
                continue;

            var entity = mind.OwnedEntity;
            var originalEntity = GetEntity(mind.OriginalOwnedEntity);

            if (HasComp<GhostComponent>(entity)) // incase they ghosted
                entity = originalEntity;

            if (!entity.HasValue)
                continue;

            if (!TryComp<SpriteComponent>(entity, out var otherSprite))
                continue;

            sprite.CopyFrom(otherSprite);
            sprite.Rotation = Angle.Zero;
            sprite.DrawDepth = (int)DrawDepth.Ghosts;
            sprite.PostShader = _prototypes.Index<ShaderPrototype>("RMCInvisible").InstanceUnique();
            sprite.PostShader.SetParameter("visibility", _opacity);

            if (HasComp<XenoComponent>(entity)) // update xeno visuals
            {
                if (sprite is { BaseRSI: { } rsi } && sprite.LayerMapTryGet(XenoVisualLayers.Base, out var layer))
                {
                    if (rsi.TryGetState("alive", out _))
                        sprite.LayerSetState(layer, "alive");

                    if (sprite.LayerMapTryGet(RMCDamageVisualLayers.Base, out var damageLayer))
                        sprite.LayerSetVisible(damageLayer, false); // set damage visuals invisible
                }
            }
        }
    }
}
