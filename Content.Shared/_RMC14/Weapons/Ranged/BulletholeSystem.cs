using Content.Shared._RMC14.Weapons.Ranged.Ammo;
using Content.Shared.Damage;
using Robust.Shared.Random;

namespace Content.Shared._RMC14.Weapons.Ranged;

public sealed class BulletholeSystem : EntitySystem
{
    [Dependency] private readonly SharedAppearanceSystem _appearance = default!;
    [Dependency] private readonly IRobustRandom _random = default!;

    // Bullethole overlays
    private const int MaxBulletholeState = 1;
    private const int MaxBulletholeCount = 24;
    private const string BulletholeRsiPath = "/Textures/_RMC14/Effects/bullethole.rsi";

    public override void Initialize()
    {
        SubscribeLocalEvent<BulletholeComponent, DamageChangedEvent>(OnVisualsDamageChangedEvent);
    }

    private void OnVisualsDamageChangedEvent(Entity<BulletholeComponent> ent, ref DamageChangedEvent args)
    {
        if (!TryComp(args.Tool, out BulletholeGeneratorComponent? bulletholeGeneratorComponent))
            return;

        ent.Comp.BulletholeCount++;

        if (!TryComp<AppearanceComponent>(ent, out var app))
            return;

        if (ent.Comp.BulletholeState < 1 || ent.Comp.BulletholeState > MaxBulletholeState)
            ent.Comp.BulletholeState = _random.Next(1, MaxBulletholeState + 1);

        var stateString = $"bhole_{ent.Comp.BulletholeState}_{(ent.Comp.BulletholeCount >= MaxBulletholeCount ? MaxBulletholeCount : ent.Comp.BulletholeCount)}";
        _appearance.SetData(ent, BulletholeVisuals.State, stateString, app);
    }
}
