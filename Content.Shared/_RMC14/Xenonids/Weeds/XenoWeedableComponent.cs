using Content.Shared._RMC14.Xenonids.Construction.Nest;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared._RMC14.Xenonids.Weeds;

/// <summary>
/// The entity that is weedable will have some change applied to it to cause it to appear covered in weeds.
/// This entity allows another entity to be spawned onto it, or use the visualizer system.
/// The appearence value "Weeded" will be appropriatly set by this mechanism.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
[Access(typeof(SharedXenoWeedsSystem))]
public sealed partial class XenoWeedableComponent : Component
{
    [DataField, AutoNetworkedField]
    public EntProtoId? Spawn;

    [DataField, AutoNetworkedField]
    [Access(typeof(SharedXenoWeedsSystem), typeof(XenoNestSystem))]
    public EntityUid? Entity;
}

[Serializable, NetSerializable]
public enum WeededEntityLayers
{
    Layer
}
