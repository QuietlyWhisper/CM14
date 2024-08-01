﻿using Robust.Client.AutoGenerated;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.XAML;
using static Robust.Client.UserInterface.Controls.BaseButton;

namespace Content.Client._RMC14.Overwatch;

[GenerateTypedNameReferences]
public sealed partial class OverwatchSquadView : Control
{
    public event Action? OnStop;

    public OverwatchSquadView()
    {
        RobustXamlLoader.Load(this);
        TabContainer.SetTabTitle(SquadMonitor, "Squad Monitor");
        StopOverwatchButton.OnPressed += OnStopOverwatchPressed;
    }

    private void OnStopOverwatchPressed(ButtonEventArgs obj)
    {
        OnStop?.Invoke();
    }
}
