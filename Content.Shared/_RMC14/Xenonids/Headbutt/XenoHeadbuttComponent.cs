﻿using System.Numerics;
using Content.Shared.Damage;
using Content.Shared.FixedPoint;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._RMC14.Xenonids.Headbutt;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
[Access(typeof(XenoHeadbuttSystem))]
public sealed partial class XenoHeadbuttComponent : Component
{
    [DataField, AutoNetworkedField]
    public FixedPoint2 PlasmaCost = 10;

    [DataField]
    public DamageSpecifier Damage = new();

    [DataField, AutoNetworkedField]
    public float Range = 3;

    [DataField, AutoNetworkedField]
    public EntProtoId Effect = "CMEffectPunch";

    [DataField, AutoNetworkedField]
    public SoundSpecifier Sound = new SoundPathSpecifier("/Audio/_RMC14/Xeno/alien_claw_block.ogg");

    [DataField, AutoNetworkedField]
    public Vector2? Charge;
}
