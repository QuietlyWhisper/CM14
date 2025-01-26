using Content.Shared._RMC14.Xenonids.Weeds;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Content.Shared._RMC14.Xenonids.Construction;

public sealed partial class HivePylonSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<HivePylonComponent, AfterEntityWeedingEvent>(OnWeedTower);
    }

    private void OnWeedTower(Entity<HivePylonComponent> hivePylon, ref AfterEntityWeedingEvent args)
    {

    }
}
