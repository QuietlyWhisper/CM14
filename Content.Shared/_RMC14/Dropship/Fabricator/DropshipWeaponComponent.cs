﻿using Robust.Shared.GameStates;

namespace Content.Shared._RMC14.Dropship.Fabricator;

[RegisterComponent, NetworkedComponent]
[Access(typeof(DropshipFabricatorSystem))]
public sealed partial class DropshipWeaponComponent : Component;
