using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._RMC14.Examine.Pose;

/// <summary>
/// Flavour text when this entity is examined. Can be set with an action.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
[Access(typeof(SharedRMCSetPoseSystem))]
public sealed partial class RMCSetPoseComponent : Component
{
    [DataField, AutoNetworkedField]
    public string Pose = string.Empty;
}
