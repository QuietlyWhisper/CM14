using Content.Shared._RMC14.Xenonids.Construction;
using Content.Shared._RMC14.Xenonids.Construction.Tunnel;
using Content.Shared._RMC14.Xenonids.Evolution;
using Content.Shared.FixedPoint;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Shared._RMC14.Xenonids.Hive;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState, AutoGenerateComponentPause]
[Access(typeof(SharedXenoHiveSystem), typeof(SharedXenoPylonSystem), typeof(SharedXenoTunnelSystem))]
public sealed partial class HiveComponent : Component
{
    [DataField, AutoNetworkedField]
    public Dictionary<int, FixedPoint2> TierLimits = new()
    {
        [2] = 0.5,
        [3] = 0.2,
    };

    [DataField, AutoNetworkedField]
    public Dictionary<EntProtoId, int> FreeSlots = new() {["CMXenoHivelord"] = 1, ["CMXenoCarrier"] = 1, ["CMXenoBurrower"] = 1};

    [DataField, AutoNetworkedField]
    public Dictionary<EntProtoId, int> HiveStructureSlots = new() { ["HiveCoreXeno"] = 1, ["HiveClusterXeno"] = 8, ["HivePylonXeno"] = 2, ["HiveEggMorpherXeno"] = 6, ["HiveRecoveryNodeXeno"] = 6 };

    [DataField, AutoNetworkedField]
    public Dictionary<TimeSpan, List<EntProtoId>> Unlocks = new();

    [DataField, AutoNetworkedField]
    public HashSet<EntProtoId> AnnouncedUnlocks = new();

    [DataField, AutoNetworkedField]
    public List<TimeSpan> AnnouncementsLeft = [];

    [DataField, AutoNetworkedField]
    public SoundSpecifier AnnounceSound = new SoundPathSpecifier("/Audio/_RMC14/Xeno/alien_distantroar_3.ogg", AudioParams.Default.WithVolume(-6));

    [DataField, AutoNetworkedField]
    public bool SeeThroughContainers;

    [DataField, AutoNetworkedField]
    public EntityUid? CurrentQueen;

    [DataField, AutoNetworkedField]
    public TimeSpan? LastQueenDeath;

    [DataField, AutoNetworkedField]
    public TimeSpan NewQueenCooldown = TimeSpan.FromMinutes(5);

    [DataField, AutoNetworkedField]
    public bool GotOvipositorPopup;

    [DataField, AutoNetworkedField]
    public TimeSpan NewCoreCooldown = TimeSpan.FromMinutes(5);

    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoNetworkedField, AutoPausedField]
    public TimeSpan NewCoreAt;

    [DataField, AutoNetworkedField]
    public bool HijackSurged;

    [DataField, AutoNetworkedField]
    public Dictionary<string, EntityUid> HiveTunnels = new();

    [DataField, AutoNetworkedField]
    public int BurrowedLarva;

    [DataField, AutoNetworkedField]
    public int BurrowedLarvaSlotFactor = 4;

    [DataField, AutoNetworkedField]
    public bool LateJoinGainLarva;

    [DataField, AutoNetworkedField]
    public FixedPoint2 LateJoinMarines;

    [DataField, AutoNetworkedField]
    public EntProtoId BurrowedLarvaId = "CMXenoLarva";
}
