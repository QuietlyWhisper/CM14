using Content.Shared._RMC14.Xenonids.Construction.ResinWhisper;
using Content.Shared.FixedPoint;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;

namespace Content.Shared._RMC14.Xenonids.Construction;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(true)]
[Access(typeof(SharedXenoConstructionSystem), typeof(ResinWhispererSystem))]
public sealed partial class XenoConstructionComponent : Component
{
    [DataField, AutoNetworkedField]
    public FixedPoint2 BuildRange = 1.9;

    [DataField, AutoNetworkedField]
    public List<EntProtoId> CanBuild = new();

    [DataField, AutoNetworkedField]
    public EntProtoId? BuildChoice;

    [DataField, AutoNetworkedField]
    public TimeSpan BuildDelay = TimeSpan.FromSeconds(4);

    [DataField, AutoNetworkedField]
    public FixedPoint2 OrderConstructionRange = 1.9;

    [DataField, AutoNetworkedField]
    public List<EntProtoId> CanOrderConstruction = new();

    [DataField, AutoNetworkedField]
    public EntityCoordinates? OrderingConstructionAt;

    [DataField, AutoNetworkedField]
    public TimeSpan OrderConstructionDelay = TimeSpan.FromSeconds(3);

    [DataField, AutoNetworkedField]
    public TimeSpan OrderConstructionAddPlasmaDelay = TimeSpan.FromSeconds(3);

    [DataField, AutoNetworkedField]
    public SoundSpecifier BuildSound = new SoundCollectionSpecifier("RMCResinBuild")
    {
        Params = AudioParams.Default.WithVolume(-10f),
    };

    [DataField, AutoNetworkedField]
    public bool CanUpgrade;

    [DataField, AutoNetworkedField]
    public Dictionary<EntProtoId, int> XenoStructureSlots = new() { };
}
