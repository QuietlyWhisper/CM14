﻿using Content.Shared.Inventory.VirtualItem;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._RMC14.PowerLoader;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
[Access(typeof(PowerLoaderSystem))]
public sealed partial class PowerLoaderGrabbableComponent : Component
{
    [DataField, AutoNetworkedField]
    public TimeSpan Delay;

    [DataField, AutoNetworkedField]
    public EntProtoId<VirtualItemComponent> VirtualRight;

    [DataField, AutoNetworkedField]
    public EntProtoId<VirtualItemComponent> VirtualLeft;
}
