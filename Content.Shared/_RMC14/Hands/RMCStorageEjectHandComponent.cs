using Robust.Shared.GameStates;

namespace Content.Shared._RMC14.Hands;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
[Access(typeof(CMHandsSystem))]
public sealed partial class RMCStorageEjectHandComponent : Component
{
    [DataField, AutoNetworkedField]
    public bool Enabled = false;
}
