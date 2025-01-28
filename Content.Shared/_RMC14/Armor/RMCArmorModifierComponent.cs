using Robust.Shared.GameStates;

namespace Content.Shared._RMC14.Armor;

[RegisterComponent, NetworkedComponent]
public sealed partial class RMCArmorModifierComponent : Component
{
    [DataField]
    public int MeleeArmorModifier = 4;

    [DataField]
    public int RangedArmorModifier = 4;
}
