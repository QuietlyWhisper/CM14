using Content.Shared._RMC14.GhostAppearance;
using Robust.Client.GameObjects;

namespace Content.Client._RMC14.GhostAppearance;

public sealed class GhostColorSystem : EntitySystem
{
    public override void Update(float frameTime)
    {
        var defaultColor = Color.FromHex("#FFFFFF88");
        var colors = EntityQueryEnumerator<GhostColorComponent, SpriteComponent>();
        while (colors.MoveNext(out var color, out var sprite))
        {
            sprite.Color = color.Color ?? defaultColor;
        }
    }
}
