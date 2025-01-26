using Content.Shared.Actions;
using Content.Shared.FixedPoint;
using Robust.Shared.Prototypes;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Content.Shared._RMC14.Xenonids.Heal;

public sealed partial class XenoSacraficeHealActionEvent : EntityTargetActionEvent
{
    [DataField]
    public float Range = 2.0F;

    /// <summary>
    /// What proportion of current health will be sent to target
    /// </summary>
    [DataField]
    public FixedPoint2 TransferProportion = 0.75;

    /// <summary>
    /// Assuming respawn, how long after death will respawn occur
    /// </summary>
    [DataField]
    public TimeSpan RespawnDelay = TimeSpan.FromSeconds(0.5);

    [DataField]
    public EntProtoId HealEffect = "RMCEffectHeal";
}
