using Content.Client._RMC14.Medical.HUD.Holocard;
using Content.Client.UserInterface.Controls;
using JetBrains.Annotations;
using Robust.Client.AutoGenerated;
using Robust.Client.UserInterface.CustomControls;
using Robust.Client.UserInterface.XAML;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Content.Client._RMC14.Chemistry.PillBottle;

[GenerateTypedNameReferences]
public sealed partial class RMCChangePillBottleColorWindow : DefaultWindow
{
    private readonly RMCChangePillBottleColorBui _owner;

    public RMCChangePillBottleColorWindow(RMCChangePillBottleColorBui owner)
    {
        RobustXamlLoader.Load(this);
        IoCManager.InjectDependencies(this);

        _owner = owner;

        Title = Loc.GetString("rmc-ui-change-pill-bottle-color-title");
    }
}

