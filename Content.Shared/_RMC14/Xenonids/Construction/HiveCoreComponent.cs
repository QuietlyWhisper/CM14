using Robust.Shared.GameStates;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Content.Shared._RMC14.Xenonids.Construction;

/// <summary>
/// Indicated this entity is a hivecore, only 1 hivecore per hive
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class HiveCoreComponent : Component
{
}
