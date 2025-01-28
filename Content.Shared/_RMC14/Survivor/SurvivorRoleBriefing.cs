using Content.Shared._RMC14.Survivor;
using Robust.Shared.GameStates;

namespace Content.Server.Roles;

/// <summary>
/// Component that refers to a freemarker/ftl string in a role's YAML files for the purposes of generating briefings. Primarily used by the survivorsystem.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class SurvivorRoleBriefingComponent : Component
{
    [DataField(required: true)]
    public LocId SurvivorRoleBriefing { get; private set; }

}
