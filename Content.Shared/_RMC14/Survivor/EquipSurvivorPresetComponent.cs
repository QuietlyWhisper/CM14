using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._RMC14.Survivor;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
[Access(typeof(SharedSurvivorSystem))]
public sealed partial class EquipSurvivorPresetComponent : Component
{
    [DataField, AutoNetworkedField]
    public EntProtoId<SurvivorPresetComponent> Preset = "RMCSurvivorPresetCivilian";
}
