using Robust.Shared.GameStates;

namespace Content.Shared._RMC14.Survivor;

[RegisterComponent, NetworkedComponent]
[Access(typeof(SharedSurvivorSystem))]
public sealed partial class SurvivorComponent : Component;
