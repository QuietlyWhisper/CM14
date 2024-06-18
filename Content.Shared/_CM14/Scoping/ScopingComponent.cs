﻿using System.Numerics;
using Robust.Shared.GameStates;

namespace Content.Shared._CM14.Scoping;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
[Access(typeof(SharedScopeSystem))]
public sealed partial class ScopingComponent : Component
{
    [ViewVariables, AutoNetworkedField]
    public EntityUid? Scope;

    [ViewVariables, AutoNetworkedField]
    public Vector2 EyeOffset;
}
