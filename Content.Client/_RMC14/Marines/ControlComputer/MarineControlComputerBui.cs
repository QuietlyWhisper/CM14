using Content.Shared._RMC14.Marines.ControlComputer;
using JetBrains.Annotations;
using Robust.Client.UserInterface;

namespace Content.Client._RMC14.Marines.ControlComputer;

[UsedImplicitly]
public sealed class MarineControlComputerBui(EntityUid owner, Enum uiKey) : BoundUserInterface(owner, uiKey)
{
    private MarineControlComputerWindow? _window;

    private bool _confirmingEvacuation;

    protected override void Open()
    {
        // TODO RMC14 this should be named Almayer/Savannah control console
        // TODO RMC14 change alert level button also needs to state current alert level
        _window = this.CreateWindow<MarineControlComputerWindow>();
        Refresh();

        _window.AlertButton.OnPressed += _ => SendPredictedMessage(new MarineControlComputerAlertLevelMsg());

        _window.MedalButton.OnPressed += _ => SendPredictedMessage(new MarineControlComputerMedalMsg());

        _window.EvacuationButton.OnPressed += _ =>
        {
            if (_confirmingEvacuation)
            {
                SendPredictedMessage(new MarineControlComputerToggleEvacuationMsg());
                _confirmingEvacuation = false;
            }
            else
            {
                _confirmingEvacuation = true;
            }

            Refresh();
        };
    }

    public void Refresh()
    {
        if (_window is not { IsOpen: true })
            return;

        if (!EntMan.TryGetComponent(Owner, out MarineControlComputerComponent? computer))
            return;

        // TODO RMC14 estimated time until escape pod launch
        if (_confirmingEvacuation)
            _window.EvacuationButton.Text = Loc.GetString("rmc-almayer-control-evacuate-confirm");
        else
            _window.EvacuationButton.Text = Loc.GetString("rmc-almayer-control-evacuate", ("evacuating", computer.Evacuating));

        _window.EvacuationButton.Disabled = !computer.CanEvacuate;
    }
}
