using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._RMC14.Xenonids.Devour;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
[Access(typeof(XenoDevourSystem))]
public sealed partial class XenoDevourComponent : Component
{
    [DataField, AutoNetworkedField]
    public TimeSpan DevourDelay = TimeSpan.FromSeconds(5);

    [DataField, AutoNetworkedField]
    public string DevourContainerId = "cm_xeno_devour";

    [DataField, AutoNetworkedField]
    public SoundSpecifier RegurgitateSound = new SoundCollectionSpecifier("XenoDrool");

    [DataField, AutoNetworkedField]
    public TimeSpan WarnAfter = TimeSpan.FromSeconds(50);

    [DataField, AutoNetworkedField]
    public TimeSpan RegurgitateAfter = TimeSpan.FromSeconds(60);

    [DataField, AutoNetworkedField]
    public TimeSpan RegurgitationStun = TimeSpan.FromSeconds(4);
}
