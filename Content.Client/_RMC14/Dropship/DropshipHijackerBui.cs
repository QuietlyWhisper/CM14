using Content.Client.Message;
using Content.Shared._RMC14.Dropship;
using JetBrains.Annotations;
using Robust.Client.UserInterface.Controls;

namespace Content.Client._RMC14.Dropship;

[UsedImplicitly]
public sealed class DropshipHijackerBui : BoundUserInterface
{
    [ViewVariables]
    private DropshipHijackerWindow? _window;

    public DropshipHijackerBui(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
    }

    protected override void Open()
    {
        if (State is DropshipHijackerBuiState s)
            Set(s);
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        if (state is DropshipHijackerBuiState s)
            Set(s);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
            _window?.Dispose();
    }

    private void Set(DropshipHijackerBuiState s)
    {
        if (_window == null)
        {
            _window = new DropshipHijackerWindow();
            _window.OnClose += Close;
            _window.Header.SetMarkup(Loc.GetString("rmc-dropship-navigation-hijack-select-place"));
            _window.OpenCentered();
        }

        _window.Destinations.DisposeAllChildren();
        foreach (var (id, name) in s.Destinations)
        {
            var button = new Button
            {
                Text = name,
                StyleClasses = { "OpenBoth" }
            };

            button.OnPressed += _ =>
            {
                SendPredictedMessage(new DropshipHijackerDestinationChosenBuiMsg(id));
                Close();
            };
            _window.Destinations.AddChild(button);
        }
    }
}
