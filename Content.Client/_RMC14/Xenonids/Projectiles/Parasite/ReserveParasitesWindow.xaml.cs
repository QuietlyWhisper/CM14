using Robust.Client.AutoGenerated;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;
using Robust.Client.UserInterface.XAML;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Robust.Shared.Prototypes;

namespace Content.Client._RMC14.Xenonids.Projectiles.Parasite;


[GenerateTypedNameReferences]
public sealed partial class ReserveParasitesWindow : DefaultWindow
{
    [Dependency] private readonly IPrototypeManager _prototypeManager = default!;
    [Dependency] private readonly IEntityManager _entityManager = default!;

    private readonly ReserveParasitesBoundUserInterface _owner;

    public ReserveParasitesWindow(ReserveParasitesBoundUserInterface owner)
    {
        IoCManager.InjectDependencies(this);
        RobustXamlLoader.Load(this);

        _owner = owner;
        Title = Loc.GetString("xeno-ui-reserve-parasites-title");
        ApplyButton.Text = Loc.GetString("xeno-ui-reserve-parasites-apply-button-text");

        ApplyButton.OnPressed += ApplyNewReserve;
    }

    private void ApplyNewReserve(BaseButton.ButtonEventArgs args)
    {
        _owner.ChangeReserve(ReserveBar.Value);
        _owner.Close();
    }

}
