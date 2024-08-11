﻿using Robust.Client.AutoGenerated;
using Robust.Client.Graphics;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.XAML;
using Robust.Shared.Utility;

namespace Content.Client._RMC14.Dropship.Weapons;

[GenerateTypedNameReferences]
public sealed partial class DropshipWeaponsButton : Button
{
    private Action<ButtonEventArgs>? _onPressed;

    public DropshipWeaponsButton()
    {
        RobustXamlLoader.Load(this);
        Label.ModulateSelfOverride = Color.White;
    }

    private StyleBoxFlat ActiveStyleBox()
    {
        return new StyleBoxFlat
        {
            BackgroundColor = Color.FromHex("#222222"),
            BorderColor = Color.FromHex("#00AE3A"),
            BorderThickness = new Thickness(3),
        };
    }

    public void Refresh()
    {
        Disabled = string.IsNullOrEmpty(FormattedMessage.RemoveMarkupPermissive(RichLabel.GetMessage() ?? string.Empty));
        var box = ActiveStyleBox();
        if (Disabled)
            box.BorderColor = Color.Black;

        StyleBoxOverride = box;
    }

    public void SetOnPressed(Action<ButtonEventArgs>? action)
    {
        OnPressed -= _onPressed;
        if (action == null)
            return;

        _onPressed = action;
        OnPressed += action;
    }
}
