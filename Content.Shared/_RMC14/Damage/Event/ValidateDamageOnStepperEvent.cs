using Robust.Shared.Serialization;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Content.Shared._RMC14.Damage.Event;

[Serializable, NetSerializable]
public sealed partial class ValidateDamageOnStepperEvent : CancellableEntityEventArgs
{
    public NetEntity SteppedEntity;
    public NetEntity PossibleTarget;

    public ValidateDamageOnStepperEvent(NetEntity steppedEntity, NetEntity possibleTarget)
    {
        SteppedEntity = steppedEntity;
        PossibleTarget = possibleTarget;
    }
}
