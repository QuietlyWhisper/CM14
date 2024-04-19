﻿using Robust.Shared.GameStates;

namespace Content.Shared._CM14.Xenos.Hugger;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
[Access(typeof(SharedXenoHuggerSystem))]
public sealed partial class XenoHuggerComponent : Component
{
    [DataField, AutoNetworkedField]
    public TimeSpan ManualAttachDelay = TimeSpan.FromSeconds(2);

    [DataField, AutoNetworkedField]
    public TimeSpan KnockdownTime = TimeSpan.FromMinutes(1.5);
}
