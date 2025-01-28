using Robust.Shared.GameStates;

namespace Content.Shared._RMC14.Slow;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class RMCSlowdownComponent : Component
{
    [DataField, AutoNetworkedField]
    public TimeSpan ExpiresAt;
}
