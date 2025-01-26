using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._RMC14.Xenonids.Weeds;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
[Access(typeof(SharedXenoWeedsSystem))]
public sealed partial class XenoWeedsComponent : Component
{
    [DataField]
    public int Range = 5;

    [DataField]
    public float SpeedMultiplierXeno = 1.05f;

    [DataField]
    public float SpeedMultiplierOutsider = 0.6f; // TODO RMC14

    [DataField, AutoNetworkedField]
    public bool IsSource = true;

    [DataField, AutoNetworkedField]
    public EntityUid? Source;

    [DataField]
    public EntProtoId Spawns = "XenoWeeds";

    [DataField, AutoNetworkedField]
    public List<EntityUid> Spread = new();

    /// <summary>
    /// All anchored entities with Weedable component adjacent to this entity
    /// are added here. 
    /// </summary>
    [DataField, AutoNetworkedField]
    public List<EntityUid> LocalWeeded = new();

    [DataField, AutoNetworkedField]
    public TimeSpan MinRandomDelete = TimeSpan.FromSeconds(9);

    [DataField, AutoNetworkedField]
    public TimeSpan MaxRandomDelete = TimeSpan.FromSeconds(10);

    [DataField, AutoNetworkedField]
    public bool SpreadsOnSemiWeedable = false;

    [DataField, AutoNetworkedField]
    public float FruitGrowthMultiplier = 1.0f;

    [DataField, AutoNetworkedField]
    public int Level = 1;

    [DataField, AutoNetworkedField]
    public bool BlockOtherWeeds;
}
