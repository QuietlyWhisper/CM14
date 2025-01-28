using Content.Client.Message;
using Content.Shared._RMC14.Dropship;
using Content.Shared.Shuttles.Systems;
using JetBrains.Annotations;
using Robust.Shared.Timing;

namespace Content.Client._RMC14.Dropship;

[UsedImplicitly]
public sealed class DropshipNavigationBui : BoundUserInterface
{
    [Dependency] private readonly IEntityManager _entities = default!;
    [Dependency] private readonly IGameTiming _timing = default!;

    [ViewVariables]
    private DropshipNavigationWindow? _window;

    private readonly Dictionary<DropshipButton, string> _destinations = new();
    private NetEntity? _selected;

    public DropshipNavigationBui(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
        IoCManager.InjectDependencies(this);
    }

    protected override void Open()
    {
        OpenWindow();
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        OpenWindow();

        switch (state)
        {
            case DropshipNavigationDestinationsBuiState s:
                Set(s);
                return;
            case DropshipNavigationTravellingBuiState s:
                Set(s);
                return;
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _window?.Close();
            _window?.Dispose();
        }
    }

    private void OpenWindow()
    {
        if (_window != null)
            return;

        _window = new DropshipNavigationWindow();
        _window.OnClose += OnClose;
        SetFlightHeader(Loc.GetString("rmc-dropship-navigation-flight-controls"));
        SetDoorHeader(Loc.GetString("rmc-dropship-navigation-lockdown"));

        if (_entities.TryGetComponent(Owner, out TransformComponent? transform) &&
            _entities.TryGetComponent(transform.ParentUid, out MetaDataComponent? metaData))
        {
            _window.Title = $"{metaData.EntityName} {_window.Title}";
        }

        _window.CancelButton.Button.OnPressed += _ =>
        {
            SetCancelLaunchDisabled(true);
            _selected = null;
            ResetDestinationButtons();
        };

        _window.LaunchButton.Button.OnPressed += _ =>
        {
            if (_selected != null)
                SendPredictedMessage(new DropshipNavigationLaunchMsg(_selected.Value));

            SetCancelLaunchDisabled(true);
            _selected = null;
            ResetDestinationButtons();
        };

        _window.LockdownButton.Button.OnPressed += _ => SendPredictedMessage(new DropshipLockdownMsg());

        _window.OpenCentered();
        _entities.System<DropshipSystem>().Uis.Add(this);
    }

    private void OnClose()
    {
        _entities.System<DropshipSystem>().Uis.Remove(this);
        Close();
    }

    private void Set(DropshipNavigationDestinationsBuiState destinations)
    {
        if (_window == null)
            return;

        SetFlightHeader(Loc.GetString("rmc-dropship-navigation-flight-controls"));

        _window.DestinationsContainer.Visible = true;
        _window.ProgressBarContainer.Visible = false;
        _window.CancelButton.Visible = true;
        _window.LaunchButton.Visible = true;

        _window.DestinationsContainer.DisposeAllChildren();

        DropshipButton DestinationButton(string name, bool disabled, Action onPressed)
        {
            var button = new DropshipButton();

            button.Text = name;
            button.Disabled = disabled;
            button.BorderColor = Color.Transparent;
            button.BorderThickness = new Thickness(0);
            button.Button.ToggleMode = true;
            button.Button.OnPressed += _ =>
            {
                button.Text = $"> {name}";
                SetCancelLaunchDisabled(false);
                onPressed();
                ResetDestinationButtons();
            };

            return button;
        }

        if (destinations.FlyBy is { } flyBy)
            _window.DestinationsContainer.AddChild(DestinationButton(Loc.GetString("rmc-dropship-navigation-flyby"), false, () => _selected = flyBy));

        _destinations.Clear();
        foreach (var destination in destinations.Destinations)
        {
            var name = destination.Name;
            if (destination.Primary)
                name += Loc.GetString("rmc-dropship-navigation-primary");

            var button = DestinationButton(name, destination.Occupied, () => _selected = destination.Id);

            _destinations[button] = name;
            _window.DestinationsContainer.AddChild(button);
        }
    }

    private void Set(DropshipNavigationTravellingBuiState travelling)
    {
        if (_window == null)
            return;

        _window.DestinationsContainer.Visible = false;
        _window.ProgressBarContainer.Visible = true;
        _window.CancelButton.Visible = false;
        _window.LaunchButton.Visible = false;
        _window.ProgressBar.Margin = new Thickness(0, 5, 0, 0);

        var time = Math.Ceiling((travelling.Time.End - _timing.CurTime).TotalSeconds);
        if (time < 0.01)
            time = 0;

        var destination = travelling.Destination;
        string Msg(string msg) => $"[color=#02E74E][bold]{msg}[/bold][/color]";

        switch (travelling.State)
        {
            case FTLState.Starting:
                SetFlightHeader(Loc.GetString("rmc-dropship-navigation-launch-in-progress"));
                _window.ProgressBarHeader.SetMarkup(Msg(Loc.GetString("rmc-dropship-navigation-launching", ("time", time), ("destination", destination))));
                _window.LockdownButton.Disabled = false;
                break;
            case FTLState.Travelling:
                SetFlightHeader(Loc.GetString("rmc-dropship-navigation-in-flight", ("destination", destination)));
                _window.ProgressBarHeader.SetMarkup(Msg(Loc.GetString("rmc-dropship-navigation-time-until-destination", ("time", time))));
                _window.LockdownButton.Disabled = true;
                break;
            case FTLState.Arriving:
                SetFlightHeader(Loc.GetString("rmc-dropship-navigation-final-approach", ("destination", destination)));
                _window.ProgressBarHeader.SetMarkup(Msg(Loc.GetString("rmc-dropship-navigation-time-until-landing", ("time", time))));
                _window.LockdownButton.Disabled = true;
                break;
            case FTLState.Cooldown:
                SetFlightHeader(Loc.GetString("rmc-dropship-navigation-refueling-in-progress"));
                _window.ProgressBarHeader.SetMarkup(Msg(Loc.GetString("rmc-dropship-navigation-ready-to-launch", ("time", time))));
                _window.LockdownButton.Disabled = false;
                break;
            default:
                return;
        }

        var startEndTime = travelling.Time;
        _window.ProgressBar.MinValue = 0;
        _window.ProgressBar.MaxValue = (float) startEndTime.Length.TotalSeconds;
        _window.ProgressBar.SetAsRatio(1 - startEndTime.ProgressAt(_timing.CurTime));
    }

    private void SetFlightHeader(string label)
    {
        _window?.Header.SetMarkup($"[color=#0BDC49][font size=16][bold]{label}[/bold][/font][/color]");
    }

    private void SetDoorHeader(string label)
    {
        _window?.DoorHeader.SetMarkup($"[color=#0BDC49][font size=16][bold]{label}[/bold][/font][/color]");
    }

    private void SetCancelLaunchDisabled(bool disabled)
    {
        if (_window == null)
            return;

        _window.CancelButton.Button.Disabled = disabled;
        _window.LaunchButton.Button.Disabled = disabled;
    }

    private void ResetDestinationButtons()
    {
        if (_window == null)
            return;

        foreach (var destination in _window.DestinationsContainer.Children)
        {
            if (destination is not DropshipButton button ||
                !_destinations.TryGetValue(button, out var name))
            {
                continue;
            }

            button.Text = name;
        }
    }

    public void Update()
    {
        if (_window == null || _window.Disposed)
            return;

        if (State is DropshipNavigationTravellingBuiState s)
            Set(s);
    }
}
