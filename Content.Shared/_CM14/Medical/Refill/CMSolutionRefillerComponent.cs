﻿using Content.Shared.Chemistry.Reagent;
using Content.Shared.FixedPoint;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Shared._CM14.Medical.Refill;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(true), AutoGenerateComponentPause]
[Access(typeof(CMRefillableSolutionSystem))]
public sealed partial class CMSolutionRefillerComponent : Component
{
    [DataField(required: true), AutoNetworkedField]
    public HashSet<ProtoId<ReagentPrototype>> Reagents = new();

    [DataField, AutoNetworkedField]
    public FixedPoint2 Current;

    [DataField, AutoNetworkedField]
    public FixedPoint2 Max;

    [DataField, AutoNetworkedField]
    public FixedPoint2 Recharge;

    [DataField, AutoNetworkedField]
    public TimeSpan RechargeCooldown;

    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoNetworkedField, AutoPausedField]
    public TimeSpan RechargeAt;
}
