using Content.Shared.Examine;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Content.Shared._RMC14.Xenonids.Construction.EggMorpher;

public abstract partial class SharedEggMorpherSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<EggMorpherComponent, ExaminedEvent>(OnExamineEvent);
    }

    private void OnExamineEvent(Entity<EggMorpherComponent> eggMorpher, ref ExaminedEvent args)
    {
        if (!HasComp<XenoComponent>(args.Examiner))
        {
            return;
        }

        using (args.PushGroup(nameof(EggMorpherComponent)))
        {
            args.PushMarkup(Loc.GetString("rmc-xeno-construction-egg-morpher-examine", ("cur_paras", eggMorpher.Comp.CurParasites), ("max_paras", eggMorpher.Comp.MaxParasites)));
        }
    }
}
