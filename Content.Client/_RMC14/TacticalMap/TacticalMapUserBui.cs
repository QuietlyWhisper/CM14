using Content.Shared._RMC14.Areas;
using Content.Shared._RMC14.TacticalMap;
using JetBrains.Annotations;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;

namespace Content.Client._RMC14.TacticalMap;

[UsedImplicitly]
public sealed class TacticalMapUserBui(EntityUid owner, Enum uiKey) : BoundUserInterface(owner, uiKey)
{
    private TacticalMapWindow? _window;
    private bool _refreshed;

    protected override void Open()
    {
        _window = this.CreateWindow<TacticalMapWindow>();

        TabContainer.SetTabTitle(_window.MapTab, Loc.GetString("rmc-tactical-map-map-tab-title"));
        TabContainer.SetTabVisible(_window.MapTab, true);

        if (EntMan.TryGetComponent(Owner, out TacticalMapUserComponent? user) &&
            EntMan.TryGetComponent(user.Map, out AreaGridComponent? areaGrid))
        {
            _window.UpdateTexture((user.Map.Value, areaGrid));
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

        var user = EntMan.GetComponentOrNull<TacticalMapUserComponent>(Owner);
        if (user != null)
        {
            _window.LastUpdateAt = user.LastAnnounceAt;
            _window.NextUpdateAt = user.NextAnnounceAt;
        }

        _window.Map.Lines.Clear();

        var lines = EntMan.GetComponentOrNull<TacticalMapLinesComponent>(Owner);
        if (lines != null)
        {
            _window.Map.Lines.AddRange(lines.MarineLines);
            _window.Map.Lines.AddRange(lines.XenoLines);
        }

        if (_refreshed)
            return;

        _window.Canvas.Lines.Clear();

        if (lines != null)
        {
            _window.Canvas.Lines.AddRange(lines.MarineLines);
            _window.Canvas.Lines.AddRange(lines.XenoLines);
        }

        if (user?.CanDraw ?? false)
        {
            TabContainer.SetTabTitle(_window.CanvasTab, Loc.GetString("rmc-tactical-map-canvas-tab-title"));
            TabContainer.SetTabVisible(_window.CanvasTab, true);
        }
        else
        {
            TabContainer.SetTabVisible(_window.CanvasTab, false);
        }

        _refreshed = true;
    }

    private void UpdateBlips()
    {
        if (_window is not { IsOpen: true })
            return;

        if (!EntMan.TryGetComponent(Owner, out TacticalMapUserComponent? user))
        {
            _window.UpdateBlips(null);
            return;
        }

        var blips = new TacticalMapBlip[user.MarineBlips.Count + user.XenoBlips.Count];
        var i = 0;

        foreach (var blip in user.MarineBlips.Values)
        {
            blips[i++] = blip;
        }

        foreach (var blip in user.XenoBlips.Values)
        {
            blips[i++] = blip;
        }

        _window.UpdateBlips(blips);
    }
}
