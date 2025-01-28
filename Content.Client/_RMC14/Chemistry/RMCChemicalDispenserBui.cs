using System.Globalization;
using Content.Client.Chemistry.Containers.EntitySystems;
using Content.Client.UserInterface.ControlExtensions;
using Content.Shared._RMC14.Chemistry;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.FixedPoint;
using JetBrains.Annotations;
using Robust.Client.GameObjects;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Client._RMC14.Chemistry;

[UsedImplicitly]
public sealed class RMCChemicalDispenserBui : BoundUserInterface
{
    [Dependency] private readonly IPrototypeManager _prototypes = default!;

    private RMCChemicalDispenserWindow? _window;

    private readonly ContainerSystem _container;
    private readonly SolutionContainerSystem _solution;
    private readonly List<(Button Button, FixedPoint2 Amount)> _dispenseButtons = new();
    private readonly List<(Button Button, FixedPoint2 Amount)> _beakerButtons = new();

    public RMCChemicalDispenserBui(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
        _container = EntMan.System<ContainerSystem>();
        _solution = EntMan.System<SolutionContainerSystem>();
    }

    protected override void Open()
    {
        _window = this.CreateWindow<RMCChemicalDispenserWindow>();
        _window.EjectBeakerButton.OnPressed += _ => SendPredictedMessage(new RMCChemicalDispenserEjectBeakerBuiMsg());

        if (EntMan.TryGetComponent(Owner, out RMCChemicalDispenserComponent? dispenser))
        {
            for (var i = 0; i < dispenser.Reagents.Length; i += 3)
            {
                var row = new BoxContainer();
                void AddButton(ProtoId<ReagentPrototype> reagentId)
                {
                    if (_prototypes.TryIndex(reagentId, out var reagentProto))
                    {
                        var reagentButton = new Button
                        {
                            Text = $"\u25bc {CultureInfo.CurrentCulture.TextInfo.ToTitleCase(reagentProto.LocalizedName)}",
                            HorizontalExpand = true,
                            StyleClasses = { "OpenBoth" },
                        };
                        reagentButton.Label.AddStyleClass("CMAlignLeft");

                        reagentButton.OnPressed += _ =>
                            SendPredictedMessage(new RMCChemicalDispenserDispenseBuiMsg(reagentId));

                        row.AddChild(reagentButton);
                    }
                }

                for (var j = i; j < i + 3; j++)
                {
                    if (dispenser.Reagents.TryGetValue(j, out var reagent))
                        AddButton(reagent);
                }

                _window.ChemicalsContainer.AddChild(row);
            }

            foreach (var setting in dispenser.Settings)
            {
                var dispenseButton = new Button
                {
                    Text = $"+ {setting.Int()}",
                    StyleClasses = { "OpenBoth" },
                    SetWidth = 45,
                    Margin = new Thickness(0, 0, 0, 3),
                    Pressed = dispenser.DispenseSetting == setting,
                };
                dispenseButton.OnPressed += _ =>
                    SendPredictedMessage(new RMCChemicalDispenserDispenseSettingBuiMsg(setting));
                _window.DispenseContainer.AddChild(dispenseButton);
                _dispenseButtons.Add((dispenseButton, setting));

                var beakerButton = new Button
                {
                    Text = $"- {setting.Int()}",
                    StyleClasses = { "OpenBoth" },
                    SetWidth = 45,
                    Margin = new Thickness(0, 0, 0, 3),
                };
                beakerButton.OnPressed += _ =>
                    SendPredictedMessage(new RMCChemicalDispenserBeakerBuiMsg(setting));
                _window.BeakerContainer.AddChild(beakerButton);
                _beakerButtons.Add((beakerButton, setting));
            }
        }

        Refresh();
    }

    public void Refresh()
    {
        if (_window is not { IsOpen: true })
            return;

        if (!EntMan.TryGetComponent(Owner, out RMCChemicalDispenserComponent? dispenser))
            return;

        var max = dispenser.MaxEnergy;
        _window.EnergyBar.MaxValue = max.Float();

        var energy = dispenser.Energy;
        _window.EnergyBar.Value = energy.Float();
        _window.EnergyLabel.Text = Loc.GetString("rmc-chem-dispenser-energy-count", ("energy", energy.Int()));

        if (!_container.TryGetContainer(Owner, dispenser.ContainerSlotId, out var container) ||
            !container.ContainedEntities.TryFirstOrNull(out var contained))
        {
            _window.BeakerStatus.Text = Loc.GetString("rmc-chem-dispenser-no-beaker");
            _window.EjectBeakerButton.Visible = false;
            _window.ContentsNone.Visible = true;
            _window.BeakerContents.Visible = false;
            _window.BeakerContents.DisposeAllChildren();

            foreach (var chemical in _window.ChemicalsContainer.GetControlOfType<Button>())
            {
                chemical.Disabled = true;
            }
        }
        else
        {
            _window.EjectBeakerButton.Visible = true;
            _window.ContentsNone.Visible = false;
            _window.BeakerContents.Visible = true;
            _window.BeakerContents.DisposeAllChildren();

            foreach (var chemical in _window.ChemicalsContainer.GetControlOfType<Button>())
            {
                chemical.Disabled = false;
            }

            var units = FixedPoint2.Zero;
            var maxUnits = FixedPoint2.Zero;
            if (_solution.TryGetMixableSolution(contained.Value, out _, out var solution))
            {
                units = solution.Volume;
                maxUnits = solution.MaxVolume;

                foreach (var reagent in solution.Contents)
                {
                    var reagentName = reagent.Reagent.Prototype;
                    if (_prototypes.TryIndex(reagentName, out ReagentPrototype? reagentProto))
                    {
                        reagentName = CultureInfo.CurrentCulture.TextInfo.ToTitleCase(reagentProto.LocalizedName);
                    }

                    _window.BeakerContents.AddChild(new Label
                    {
                        Text = (Loc.GetString("rmc-chem-dispenser-beaker-units", ("units", reagent.Quantity), ("maxUnits", reagentName))),
                    });
                }
            }

            _window.BeakerStatus.Text = (Loc.GetString("rmc-chem-dispenser-beaker-reagent", ("quantity", units), ("reagent", maxUnits)));
        }

        foreach (var (button, amount) in _dispenseButtons)
        {
            button.Pressed = dispenser.DispenseSetting == amount;
        }
    }
}
