using Content.Server._RMC14.Rules;
using Content.Server.Database;
using Content.Server.GameTicking;
using Content.Shared._RMC14.CCVar;
using Content.Shared._RMC14.GhostAppearance;
using Content.Shared._RMC14.LinkAccount;
using Content.Shared.Ghost;
using Robust.Server.Player;
using Robust.Shared.Configuration;
using Robust.Shared.Network;
using Robust.Shared.Player;
using Robust.Shared.Timing;

namespace Content.Server._RMC14.LinkAccount;

public sealed class LinkAccountSystem : EntitySystem
{
    [Dependency] private readonly IConfigurationManager _config = default!;
    [Dependency] private readonly IServerDbManager _db = default!;
    [Dependency] private readonly LinkAccountManager _linkAccount = default!;
    [Dependency] private readonly IPlayerManager _player = default!;
    [Dependency] private readonly IGameTiming _timing = default!;

    private TimeSpan _timeBetweenLobbyMessages;
    private TimeSpan _nextLobbyMessageTime;
    private TimeSpan _lobbyMessageInitialDelay;
    private (string Message, string User)? _nextLobbyMessage;
    private string? _nextMarineShoutout;
    private string? _nextXenoShoutout;

    public override void Initialize()
    {
        SubscribeLocalEvent<GameRunLevelChangedEvent>(OnGameRunLevelChanged);
        SubscribeLocalEvent<RoundEndTextAppendEvent>(OnRoundEndTextAppend, after: [typeof(CMDistressSignalRuleSystem)]);

        SubscribeLocalEvent<GhostColorComponent, PlayerAttachedEvent>(OnGhostColorPlayerAttached);

        Subs.CVar(_config, RMCCVars.RMCPatronLobbyMessageTimeSeconds, v => _timeBetweenLobbyMessages = TimeSpan.FromSeconds(v), true);
        Subs.CVar(_config, RMCCVars.RMCPatronLobbyMessageInitialDelaySeconds, v => _lobbyMessageInitialDelay = TimeSpan.FromSeconds(v), true);

        ReloadPatrons();
        GetRandomLobbyMessage();
        GetRandomShoutout();

        _linkAccount.PatronUpdated += OnPatronUpdated;
    }

    public override void Shutdown()
    {
        _linkAccount.PatronUpdated -= OnPatronUpdated;
    }

    private void OnGameRunLevelChanged(GameRunLevelChangedEvent ev)
    {
        switch (ev.New)
        {
            case GameRunLevel.InRound:
                GetRandomShoutout();
                break;
            case GameRunLevel.PreRoundLobby:
            case GameRunLevel.PostRound:
                ReloadPatrons();
                GetRandomLobbyMessage();
                GetRandomShoutout();
                break;
        }

        if (ev.New == GameRunLevel.PreRoundLobby)
            _nextLobbyMessageTime = _timing.RealTime + _lobbyMessageInitialDelay;
    }

    private void OnRoundEndTextAppend(RoundEndTextAppendEvent ev)
    {
        if (_nextMarineShoutout != null)
        {
            ev.AddLine("\n");
            ev.AddLine(Loc.GetString("rmc-ui-shoutout-marine", ("name", _nextMarineShoutout)));
        }

        if (_nextXenoShoutout != null)
        {
            ev.AddLine("\n");
            ev.AddLine(Loc.GetString("rmc-ui-shoutout-xeno", ("name", _nextXenoShoutout)));
        }
    }

    private void OnGhostColorPlayerAttached(Entity<GhostColorComponent> ent, ref PlayerAttachedEvent args)
    {
        if (!TryComp(ent, out ActorComponent? actor) ||
            _linkAccount.GetPatron(actor.PlayerSession.UserId) is not { } patron ||
            patron.Tier is not { GhostColor: true } ||
            patron.GhostColor is not { } color)
        {
            RemCompDeferred<GhostColorComponent>(ent);
            return;
        }

        ent.Comp.Color = color;
        Dirty(ent);
    }

    private async void ReloadPatrons()
    {
        try
        {
            await _linkAccount.RefreshAllPatrons();
            _linkAccount.SendPatronsToAll();
        }
        catch (Exception e)
        {
            Log.Error($"Error reloading Patrons list:\n{e}");
        }
    }

    private async void GetRandomLobbyMessage()
    {
        try
        {
            _nextLobbyMessage = await _db.GetRandomLobbyMessage();
        }
        catch (Exception e)
        {
            Log.Error($"Error getting random lobby message:\n{e}");
        }
    }

    private async void GetRandomShoutout()
    {
        try
        {
            (_nextMarineShoutout, _nextXenoShoutout) = await _db.GetRandomShoutout();
        }
        catch (Exception e)
        {
            Log.Error($"Error getting random shoutout:\n{e}");
        }
    }

    private void OnPatronUpdated((NetUserId Id, SharedRMCPatronFull Patron) tuple)
    {
        if (_player.TryGetSessionById(tuple.Id, out var session) &&
            session.AttachedEntity is { } ent &&
            HasComp<GhostComponent>(ent))
        {
            var color = EnsureComp<GhostColorComponent>(ent);
            color.Color = tuple.Patron.GhostColor;
            Dirty(ent, color);
        }
    }

    public override void Update(float frameTime)
    {
        var time = _timing.RealTime;
        if (time < _nextLobbyMessageTime)
            return;

        _nextLobbyMessageTime = time + _timeBetweenLobbyMessages;

        if (_nextLobbyMessage is { } message)
            RaiseNetworkEvent(new SharedRMCDisplayLobbyMessageEvent(message.Message, message.User));

        GetRandomLobbyMessage();
    }
}
