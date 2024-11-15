using Content.Shared._RMC14.Xenonids.Damage;
using Robust.Shared.GameStates;

namespace Content.Shared._RMC14.Weapons.Ranged;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
[Access(typeof(BulletholeVisualsSystem))]
public sealed partial class BulletholeVisualsComponent : Component
{
    [DataField, AutoNetworkedField]
    public int BulletholeCount = 0;

    [DataField, AutoNetworkedField]
    public int BulletholeState;
}
