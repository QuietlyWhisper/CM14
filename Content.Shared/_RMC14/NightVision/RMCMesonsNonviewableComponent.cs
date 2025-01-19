using Robust.Shared.GameStates;

namespace Content.Shared._RMC14.NightVision;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class RMCMesonsNonviewableComponent : Component
{
    [DataField, AutoNetworkedField]
    public bool XenoVisible;
}
