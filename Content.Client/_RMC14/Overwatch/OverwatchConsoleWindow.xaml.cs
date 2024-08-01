﻿using Robust.Client.AutoGenerated;
using Robust.Client.UserInterface.CustomControls;
using Robust.Client.UserInterface.XAML;

namespace Content.Client._RMC14.Overwatch;

[GenerateTypedNameReferences]
public sealed partial class OverwatchConsoleWindow : DefaultWindow
{
    public OverwatchConsoleWindow()
    {
        RobustXamlLoader.Load(this);
    }
}
