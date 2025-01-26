using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Content.Shared._RMC14.Xenonids.Weeds;

public sealed partial class AfterEntityWeedingEvent : EntityEventArgs
{
    public NetEntity Weeds;
    public NetEntity CoveredEntity;

    public AfterEntityWeedingEvent(NetEntity weeds, NetEntity coveredEntity)
    {
        Weeds = weeds;
        CoveredEntity = coveredEntity;
    }
}
