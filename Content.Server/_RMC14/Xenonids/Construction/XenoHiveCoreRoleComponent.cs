namespace Content.Server._RMC14.Xenonids.Construction;

[RegisterComponent]
[Access(typeof(XenoPylonSystem))]
public sealed partial class XenoHiveCoreRoleComponent : Component
{
    [DataField]
    public EntityUid? Core;
}
