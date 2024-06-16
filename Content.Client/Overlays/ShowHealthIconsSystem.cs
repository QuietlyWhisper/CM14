using System.Linq;
using Content.Client._CM14.Medical.HUD;
using Content.Client._CM14.Medical.HUD.Holocard;
using Content.Shared._CM14.Medical.Components;
using Content.Shared.Atmos.Rotting;
using Content.Shared.Damage;
using Content.Shared.Inventory.Events;
using Content.Shared.Mobs.Components;
using Content.Shared.Overlays;
using Content.Shared.StatusIcon;
using Content.Shared.StatusIcon.Components;
using Robust.Shared.Prototypes;

namespace Content.Client.Overlays;

/// <summary>
/// Shows a healthy icon on mobs.
/// </summary>
public sealed class ShowHealthIconsSystem : EquipmentHudSystem<ShowHealthIconsComponent>
{
    [Dependency] private readonly IPrototypeManager _prototypeMan = default!;
    [Dependency] private readonly CMHealthIconsSystem _healthIcons = default!;
    [Dependency] private readonly HolocardSystem _holocard = default!;

    public HashSet<string> DamageContainers = new();

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<DamageableComponent, GetStatusIconsEvent>(OnGetStatusIconsEvent);
        SubscribeLocalEvent<HolocardComponent, GetStatusIconsEvent>(OnGetStatusIconsEvent);
    }

    protected override void UpdateInternal(RefreshEquipmentHudEvent<ShowHealthIconsComponent> component)
    {
        base.UpdateInternal(component);

        foreach (var damageContainerId in component.Components.SelectMany(x => x.DamageContainers))
        {
            DamageContainers.Add(damageContainerId);
        }
    }

    protected override void DeactivateInternal()
    {
        base.DeactivateInternal();

        DamageContainers.Clear();
    }

    private void OnGetStatusIconsEvent(Entity<DamageableComponent> entity, ref GetStatusIconsEvent args)
    {
        if (!IsActive)
            return;

        var healthIcons = _healthIcons.GetIcons(entity);

        args.StatusIcons.AddRange(healthIcons);
    }

    private void OnGetStatusIconsEvent(Entity<HolocardComponent> entity, ref GetStatusIconsEvent args)
    {
        if (!IsActive)
            return;

        var holocardIcons = _holocard.GetIcons(entity);

        args.StatusIcons.AddRange(holocardIcons);
    }

    private IReadOnlyList<StatusIconPrototype> DecideHealthIcons(Entity<DamageableComponent> entity)
    {
        var damageableComponent = entity.Comp;

        if (damageableComponent.DamageContainerID == null ||
            !DamageContainers.Contains(damageableComponent.DamageContainerID))
        {
            return Array.Empty<StatusIconPrototype>();
        }

        var result = new List<StatusIconPrototype>();

        // Here you could check health status, diseases, mind status, etc. and pick a good icon, or multiple depending on whatever.
        if (damageableComponent?.DamageContainerID == "Biological")
        {
            if (TryComp<MobStateComponent>(entity, out var state))
            {
                // Since there is no MobState for a rotting mob, we have to deal with this case first.
                if (HasComp<RottingComponent>(entity) && _prototypeMan.TryIndex(damageableComponent.RottingIcon, out var rottingIcon))
                    result.Add(rottingIcon);
                else if (damageableComponent.HealthIcons.TryGetValue(state.CurrentState, out var value) && _prototypeMan.TryIndex(value, out var icon))
                    result.Add(icon);
            }
        }

        return result;
    }
}
