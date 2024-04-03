﻿using Robust.Shared.GameStates;

namespace Content.Shared._CM14.Medical.Surgery.Effects;

[RegisterComponent, NetworkedComponent]
[Access(typeof(SharedCMSurgerySystem))]
public sealed partial class CMSurgeryRemoveLarvaComponent : Component;
