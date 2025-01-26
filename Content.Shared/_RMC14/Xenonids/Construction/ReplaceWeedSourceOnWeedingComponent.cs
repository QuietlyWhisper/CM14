using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Content.Shared._RMC14.Xenonids.Construction;

/// <summary>
/// Once one of the weeds that was spawned from this entity weeds the same tile as a particular entity,
/// this entity is replaced with a different entity, which acts as the new source for the weeds.
///
/// NOTE: THE NEW ENTITY WILL NOT SPREAD WEEDS. The original entity's weeds will continue to spread,
/// and use the new Entity's range.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class ReplaceWeedSourceOnWeedingComponent : Component
{
    /// <summary>
    /// Once an entity with [Key] protoId is weeded,
    /// this entity is replaced with a new Entity with [Value] protoId
    /// </summary>
    [DataField]
    public Dictionary<EntProtoId, EntProtoId> ReplacementPairs;
}
