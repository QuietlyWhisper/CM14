using Content.Shared.Alert;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared._RMC14.EyeProtection;

/// <summary>
///     Keeps track of whether eye protection is enabled or not.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(true)]
[Access(typeof(RMCSharedEyeProtectionSystem))]
public sealed partial class RMCEyeProtectionComponent : Component
{
    [DataField]
    public ProtoId<AlertPrototype>? Alert;

    /// <summary>
    ///     Is eye protection enabled?
    /// </summary>
    [DataField, AutoNetworkedField]
    public EyeProtectionState State = EyeProtectionState.On;

    [DataField, AutoNetworkedField]
    public bool Overlay;
}

[Serializable, NetSerializable]
public enum EyeProtectionState
{
    Off,
    On
}
