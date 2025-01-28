using Content.Shared._RMC14.Areas;
using Content.Shared._RMC14.Marines.Skills;
using Content.Shared._RMC14.TacticalMap;
using JetBrains.Annotations;
using Robust.Client.Player;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;

namespace Content.Client._RMC14.TacticalMap;

[UsedImplicitly]
public sealed class TacticalMapComputerBui(EntityUid owner, Enum uiKey) : BoundUserInterface(owner, uiKey)
{
    [Dependency] private readonly IPlayerManager _player = default!;

    private TacticalMapWindow? _window;
    private bool _refreshed;

    protected override void Open()
    {
        _window = this.CreateWindow<TacticalMapWindow>();

        TabContainer.SetTabTitle(_window.MapTab, Loc.GetString("rmc-tactical-map-map-tab-title"));
        TabContainer.SetTabVisible(_window.MapTab, true);

        var computer = EntMan.GetComponentOrNull<TacticalMapComputerComponent>(Owner);
        var skills = EntMan.System<SkillsSystem>();
        if (computer != null &&
            _player.LocalEntity is { } player &&
            skills.HasSkill(player, computer.Skill, computer.SkillLevel))
        {
            TabContainer.SetTabTitle(_window.CanvasTab, Loc.GetString("rmc-tactical-map-canvas-tab-title"));
            TabContainer.SetTabVisible(_window.CanvasTab, true);
        }
        else
        {
            TabContainer.SetTabVisible(_window.CanvasTab, false);
        }

        if (computer != null &&
            EntMan.TryGetComponent(computer.Map, out AreaGridComponent? areaGrid))
        {
            _window.UpdateTexture((computer.Map.Value, areaGrid));
        }

        Refresh();

        _window.UpdateCanvasButton.OnPressed += _ => SendPredictedMessage(new TacticalMapUpdateCanvasMsg(_window.Canvas.Lines));
    }

    public void Refresh()
    {
        if (_window is not { IsOpen: true })
            return;

        var lineLimit = EntMan.System<TacticalMapSystem>().LineLimit;
        _window.SetLineLimit(lineLimit);
        UpdateBlips();

        if (EntMan.TryGetComponent(Owner, out TacticalMapComputerComponent? computer))
        {
            _window.LastUpdateAt = computer.LastAnnounceAt;
            _window.NextUpdateAt = computer.NextAnnounceAt;
        }

        _window.Map.Lines.Clear();

        var lines = EntMan.GetComponentOrNull<TacticalMapLinesComponent>(Owner);
        if (lines != null)
            _window.Map.Lines.AddRange(lines.MarineLines);

        if (_refreshed)
            return;

        if (lines != null)
            _window.Canvas.Lines.AddRange(lines.MarineLines);

        _refreshed = true;
    }

    private void UpdateBlips()
    {
        if (_window is not { IsOpen: true })
            return;

        if (!EntMan.TryGetComponent(Owner, out TacticalMapComputerComponent? computer))
        {
            _window.UpdateBlips(null);
            return;
        }

        var blips = new TacticalMapBlip[computer.Blips.Count];
        var i = 0;

        foreach (var blip in computer.Blips.Values)
        {
            blips[i++] = blip;
        }

        _window.UpdateBlips(blips);
    }
}
