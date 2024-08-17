using Content.Server.ExpendableLightSystem.cs;
using Content.Shared.Tag;

namespace Content.Shared._RMC14.Light;

public sealed class FlareLitSystem : ExpendableLightSystem
{
  public override void Initialize()
  {
    if (ExpendableLightState.Lit) 
    {
      _tagSystem.AddTag(ent, "Trash");
    }
  }
}
/// ExpendableLightSystem Already marks Fading/Spent as Trash, this is just to add it to Lit Flares
