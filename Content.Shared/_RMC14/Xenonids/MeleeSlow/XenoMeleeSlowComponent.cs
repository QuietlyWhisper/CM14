using Content.Shared.FixedPoint;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._RMC14.Xenonids.MeleeSlow;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
[Access(typeof(XenoMeleeSlowSystem))]
public sealed partial class XenoMeleeSlowComponent : Component
{
    [DataField, AutoNetworkedField]
    public TimeSpan SlowTime = TimeSpan.FromSeconds(3.5);

    [DataField, AutoNetworkedField]
    public bool RequiresKnockDown = false;
}
