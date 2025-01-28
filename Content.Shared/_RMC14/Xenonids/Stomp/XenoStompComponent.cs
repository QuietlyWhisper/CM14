using System.Numerics;
using Content.Shared.Damage;
using Content.Shared.FixedPoint;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._RMC14.Xenonids.Stomp;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
[Access(typeof(XenoStompSystem))]
public sealed partial class XenoStompComponent : Component
{
    [DataField, AutoNetworkedField]
    public FixedPoint2 PlasmaCost = 30;

    [DataField]
    public DamageSpecifier Damage = new();

    [DataField, AutoNetworkedField]
    public TimeSpan ParalyzeTime = TimeSpan.FromSeconds(0.4);

    [DataField, AutoNetworkedField]
    public bool Slows = true;

    [DataField, AutoNetworkedField]
    public TimeSpan SlowTime = TimeSpan.FromSeconds(1);

    [DataField, AutoNetworkedField]
    public float ShortRange = 0.5f;

    [DataField, AutoNetworkedField]
    public float Range = 2.82f;

    [DataField, AutoNetworkedField]
    public EntProtoId? SelfEffect;

    // TODO RMC14 bang.ogg
    [DataField, AutoNetworkedField]
    public SoundSpecifier Sound = new SoundPathSpecifier("/Audio/_RMC14/Xeno/alien_footstep_charge1.ogg");
}
