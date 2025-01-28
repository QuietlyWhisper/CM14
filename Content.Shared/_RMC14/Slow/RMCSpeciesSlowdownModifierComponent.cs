using Robust.Shared.GameStates;
namespace Content.Shared._RMC14.Slow;

[RegisterComponent, NetworkedComponent]

public sealed partial class RMCSpeciesSlowdownModifierComponent : Component
{
    [DataField]
    public float SlowMultiplier;

    [DataField]
    public float SuperSlowMultiplier;

    [DataField]
    public float DurationMultiplier = 1.0f;
}
