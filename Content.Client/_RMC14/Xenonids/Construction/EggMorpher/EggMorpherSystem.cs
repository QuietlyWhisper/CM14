using Content.Shared._RMC14.Xenonids.Construction.EggMorpher;
using Content.Shared.Interaction;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Content.Client._RMC14.Xenonids.Construction.EggMorpher;

public sealed partial class EggMorpherSystem : SharedEggMorpherSystem
{
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<EggMorpherComponent, InteractHandEvent>(OnInteractHand);
        SubscribeLocalEvent<EggMorpherComponent, InteractUsingEvent>(OnInteractUsing);
    }

    private void OnInteractHand(Entity<EggMorpherComponent> eggMorpher, ref InteractHandEvent args)
    {
        args.Handled = true;
    }
    private void OnInteractUsing(Entity<EggMorpherComponent> eggMorpher, ref InteractUsingEvent args)
    {
        args.Handled = true;
    }
}
