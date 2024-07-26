﻿using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Shared._RMC14.Xenonids.Construction;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState, AutoGenerateComponentPause]
[Access(typeof(SharedXenoConstructionSystem), typeof(SharedXenoHiveCoreSystem))]
public sealed partial class HiveCoreComponent : Component
{
    [DataField, AutoNetworkedField]
    public EntProtoId Spawns = "XenoHiveWeeds";

    [DataField]
    public int MinimumLesserDrones = 2;

    [DataField]
    public int XenosPerLesserDrone = 3;

    [DataField]
    public int CurrentLesserDrones;

    [DataField]
    public int MaxLesserDrones;

    [DataField]
    public List<EntityUid> LiveLesserDrones = new();

    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoPausedField]
    public TimeSpan NextLesserDroneAt;

    [DataField]
    public TimeSpan NextLesserDroneOviCooldown = TimeSpan.FromSeconds(10);

    [DataField]
    public TimeSpan NextLesserDroneCooldown = TimeSpan.FromSeconds(125);

    /// <summary>
    /// How long a new construct can be made after the core is destroyed.
    /// Only applies to this core's hive for xeno v xeno.
    /// </summary>
    [DataField]
    public TimeSpan NewConstructCooldown = TimeSpan.FromMinutes(5);
}
