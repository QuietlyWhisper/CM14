using Robust.Shared.GameStates;


namespace Content.Shared._CM14.Attachable;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
[Access(typeof(SharedAttachableWeaponRangedModsSystem))]
public sealed partial class AttachableWeaponRangedModsWieldedComponent : Component
{
    [DataField("modifiers", required:true), AutoNetworkedField]
    public AttachableWeaponRangedModifierSet Modifiers;
}
