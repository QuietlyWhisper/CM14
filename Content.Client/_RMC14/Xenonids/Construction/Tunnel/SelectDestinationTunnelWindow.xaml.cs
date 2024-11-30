using Robust.Client.AutoGenerated;
using Robust.Client.UserInterface.CustomControls;
using Robust.Client.UserInterface.XAML;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Content.Client._RMC14.Xenonids.Construction.Tunnel;
[GenerateTypedNameReferences]
public sealed partial class SelectDestinationTunnelWindow : DefaultWindow
{
    public SelectDestinationTunnelWindow()
    {
        RobustXamlLoader.Load(this);
        IoCManager.InjectDependencies(this);

        Title = Loc.GetString("xeno-ui-select-destination-tunnel-title");
        SelectButton.Text = Loc.GetString("xeno-ui-select-destination-tunnel-submit-text");
    }
}
