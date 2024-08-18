using Content.Shared.Whitelist;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._RMC14.Armor.ThermalCloak;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class ThermalCloakComponent : Component
{
    [DataField, AutoNetworkedField]
    public EntityWhitelist Whitelist = new();

    public bool Enabled;

    [DataField, AutoNetworkedField]
    public TimeSpan Cooldown = TimeSpan.FromSeconds(3);

    [DataField, AutoNetworkedField]
    public TimeSpan ForcedCooldown = TimeSpan.FromSeconds(10);

    [DataField, AutoNetworkedField]
    public float Opacity = 0.15f;

    [DataField, AutoNetworkedField]
    public SoundSpecifier? CloakSound;

    [DataField, AutoNetworkedField]
    public SoundSpecifier? UncloakSound;

    [DataField, AutoNetworkedField]
    public bool RestrictWeapons;

    [DataField, AutoNetworkedField]
    public bool HideNightVision = true;

    [DataField, AutoNetworkedField]
    public bool BlockFriendlyFire = true;

    /// <summary>
    /// Amount of time after uncloaking weapons remain locked
    /// </summary>
    [DataField]
    [AutoNetworkedField]
    public TimeSpan UncloakWeaponLock = TimeSpan.FromSeconds(1);

    [DataField, AutoNetworkedField]
    public EntProtoId ActionId = "RMCActionToggleCloak";

    [DataField, AutoNetworkedField]
    public EntityUid? Action;
}
