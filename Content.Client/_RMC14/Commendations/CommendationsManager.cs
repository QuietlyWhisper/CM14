using System.Linq;
using Content.Shared._RMC14.Commendations;
using Robust.Shared.Network;

namespace Content.Client._RMC14.Commendations;

public sealed class CommendationsManager : IPostInjectInit
{
    [Dependency] private readonly INetManager _net = default!;

    private CommendationsWindow? _window;
    private readonly List<Commendation> _commendations = new();

    public void PostInject()
    {
        _net.RegisterNetMessage<CommendationsMsg>(OnCommendations);
    }

    private void OnCommendations(CommendationsMsg message)
    {
        _commendations.Clear();
        _commendations.AddRange(message.Commendations.OrderByDescending(c => c.Round));
    }

    public void OpenWindow()
    {
        if (_window != null)
        {
            _window.MoveToFront();
            return;
        }

        _window = new CommendationsWindow();
        _window.OnClose += () => _window = null;
        _window.OpenCentered();

        foreach (var commendation in _commendations)
        {
            var container = new CommendationContainer();
            container.Title.Text = Loc.GetString("rmc-medals-title", ("round", commendation.Round), ("name", commendation.Name));
            container.Description.Text = Loc.GetString("rmc-medals-description", ("receiver", commendation.Receiver), ("giver", commendation.Giver), ("text", commendation.Text));
            _window.Commendations.AddChild(container);
        }
    }
}
