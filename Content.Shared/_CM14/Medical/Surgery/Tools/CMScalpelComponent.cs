﻿using Robust.Shared.GameStates;

namespace Content.Shared._CM14.Medical.Surgery.Tools;

[RegisterComponent, NetworkedComponent]
[Access(typeof(SharedCMSurgerySystem))]
public sealed partial class CMScalpelComponent : Component, ICMSurgeryToolComponent
{
    public string ToolName => "a scalpel";
}
