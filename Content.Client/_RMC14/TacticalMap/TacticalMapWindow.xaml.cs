﻿using Content.Shared._RMC14.Areas;
using Robust.Client.AutoGenerated;
using Robust.Client.UserInterface.CustomControls;
using Robust.Client.UserInterface.XAML;
using static Content.Shared._RMC14.TacticalMap.TacticalMapComponent;

namespace Content.Client._RMC14.TacticalMap;

[GenerateTypedNameReferences]
public sealed partial class TacticalMapWindow : DefaultWindow
{
    private readonly List<(string Name, Color Color)> _colors = new()
    {
        ("Black", Color.Black),
        ("Red", Color.Red),
        ("Orange", Color.Orange),
        ("Blue", Color.Blue),
        ("Purple", Color.Purple),
        ("Green", Color.Green),
        ("Brown", Color.Brown),
    };

    public TacticalMapWindow()
    {
        RobustXamlLoader.Load(this);
        ClearCanvasButton.OnPressed += _ => Canvas.Lines.Clear();
        Canvas.Color = Color.Black;

        for (var i = 0; i < _colors.Count; i++)
        {
            var (name, color) = _colors[i];
            ColorsButton.AddItem(name, i);
            ColorsButton.SetItemMetadata(i, color);
        }

        ColorsButton.OnItemSelected += args =>
        {
            if (args.Button.GetItemMetadata(args.Id) is not { } metaData)
                return;

            Canvas.Color = (Color) metaData;
            ColorsButton.SelectId(args.Id);
        };
    }

    public void UpdateTexture(Entity<AreaGridComponent> grid)
    {
        Map.UpdateTexture(grid);
        Canvas.UpdateTexture(grid);
    }

    public void UpdateBlips(TacticalMapBlip[]? blips)
    {
        Map.UpdateBlips(blips);
        Canvas.UpdateBlips(blips);
    }

    public void SetLineLimit(int limit)
    {
        Canvas.LineLimit = limit;
    }
}
