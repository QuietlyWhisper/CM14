﻿namespace Content.Shared._RMC14.Visor;

[ByRefEvent]
public readonly record struct ActivateVisorEvent(Entity<CycleableVisorComponent> CycleableVisor, EntityUid User);
