using Content.Shared.FixedPoint;
using Content.Shared.StatusEffect;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._RMC14.Xenonids.Aid;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
[Access(typeof(XenoAidSystem))]
public sealed partial class XenoAidComponent : Component
{
    [DataField, AutoNetworkedField]
    public FixedPoint2 Heal = 150;

    [DataField, AutoNetworkedField]
    public int EnergyCost = 100;

    [DataField, AutoNetworkedField]
    public EntProtoId? HealEffect = "RMCEffectHeal";

    [DataField, AutoNetworkedField]
    public float AilmentsRange = 8;

    [DataField, AutoNetworkedField]
    public EntProtoId? AilmentsEffects = "RMCEffectHealAilments";

    [DataField, AutoNetworkedField]
    public TimeSpan AilmentsJitterDuration = TimeSpan.FromSeconds(1);

    [DataField, AutoNetworkedField]
    public ProtoId<StatusEffectPrototype>[] AilmentsRemove = ["KnockedDown", "Stun"];

    [DataField]
    public ComponentRegistry ComponentsRemove;
}
