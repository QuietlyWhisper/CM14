using Robust.Shared.GameStates;

namespace Content.Shared._RMC14.Pulling;

[RegisterComponent, NetworkedComponent]
[Access(typeof(RMCPullingSystem))]
public sealed partial class IgnoreBlockPullingDeadComponent : Component;
