﻿using Robust.Client.AutoGenerated;
using Robust.Client.UserInterface.CustomControls;
using Robust.Client.UserInterface.XAML;

namespace Content.Client._RMC14.Dropship.Weapons;

[GenerateTypedNameReferences]
public sealed partial class DropshipWeaponsWindow : DefaultWindow
{
    public DropshipWeaponsWindow()
    {
        RobustXamlLoader.Load(this);
    }
}
