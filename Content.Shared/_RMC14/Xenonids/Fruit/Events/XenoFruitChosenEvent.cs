﻿using Robust.Shared.Prototypes;

namespace Content.Shared._RMC14.Xenonids.Fruit.Events;

[ByRefEvent]
public readonly record struct XenoFruitChosenEvent(EntProtoId Choice);