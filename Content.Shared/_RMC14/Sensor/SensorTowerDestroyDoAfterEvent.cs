﻿using Content.Shared.DoAfter;
using Robust.Shared.Serialization;

namespace Content.Shared._RMC14.Sensor;

[Serializable, NetSerializable]
public sealed partial class SensorTowerDestroyDoAfterEvent : SimpleDoAfterEvent;
