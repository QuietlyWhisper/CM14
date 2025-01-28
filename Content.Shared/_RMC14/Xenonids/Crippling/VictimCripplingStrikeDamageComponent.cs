using Robust.Shared.GameStates;

namespace Content.Shared._RMC14.Xenonids.Crippling;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState, AutoGenerateComponentPause]
[Access(typeof(XenoCripplingStrikeSystem))]
public sealed partial class VictimCripplingStrikeDamageComponent : Component
{
    [DataField, AutoNetworkedField]
    public float DamageMult = 1;
}
