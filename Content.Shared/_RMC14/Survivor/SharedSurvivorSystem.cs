using Content.Shared.Inventory;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Shared._RMC14.Survivor;

public abstract class SharedSurvivorSystem : EntitySystem
{
    [Dependency] private readonly IComponentFactory _compFactory = default!;
    [Dependency] private readonly InventorySystem _inventory = default!;
    [Dependency] private readonly IPrototypeManager _prototypes = default!;
    [Dependency] private readonly IRobustRandom _random = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<EquipSurvivorPresetComponent, MapInitEvent>(OnPresetMapInit);
    }

    public virtual void OnPresetMapInit(Entity<EquipSurvivorPresetComponent> ent, ref MapInitEvent args)
    {
        ApplyPreset(ent, ent.Comp.Preset);
    }

    private void ApplyPreset(EntityUid mob, EntProtoId<SurvivorPresetComponent> preset)
    {
        if (!preset.TryGet(out var comp, _prototypes, _compFactory))
            return;

        if (_random.Prob(comp.PrimaryWeaponChance) &&
            comp.PrimaryWeapons.Count > 0)
        {
            var gear = _random.Pick(comp.PrimaryWeapons);
            foreach (var item in gear)
            {
                _inventory.SpawnItemOnEntity(mob, item);
            }
        }

        if (comp.RandomWeapon.Count > 0)
        {
            var gear = _random.Pick(comp.RandomWeapon);
            foreach (var item in gear)
            {
                _inventory.SpawnItemOnEntity(mob, item);
            }
        }

        if (comp.RandomGear.Count > 0)
        {
            var gear = _random.Pick(comp.RandomGear);
            foreach (var item in gear)
            {
                _inventory.SpawnItemOnEntity(mob, item);
            }
        }
    }
}
