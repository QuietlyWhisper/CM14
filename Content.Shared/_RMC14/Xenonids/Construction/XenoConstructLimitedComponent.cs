using Robust.Shared.Prototypes;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Content.Shared._RMC14.Xenonids.Construction;

/// <summary>
/// Indicates that an entity is limited by the number of said entity associated with a specific xeno.
/// Limits are found in <see cref="XenoConstructionComponent"/>
/// </summary>
[RegisterComponent]
public sealed partial class XenoConstructLimitedComponent : Component
{
    public EntityUid? Builder;
}
