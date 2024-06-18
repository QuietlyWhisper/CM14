using Content.Shared.Chemistry.Reagent;
using Robust.Client.AutoGenerated;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;
using Robust.Client.UserInterface.XAML;
using Robust.Shared.Prototypes;

namespace Content.Client._CM14.Medical.HUD.Holocard;

/// <summary>
///     A window that allows you to change the holocard of the associated entity
/// </summary>
[GenerateTypedNameReferences]
public sealed partial class HolocardChangeWindow : DefaultWindow
{
    [Dependency] private readonly IPrototypeManager _prototypeManager = default!;
    [Dependency] private readonly IEntityManager _entityManager = default!;

    private readonly EntityUid _targetEntity;
    private readonly HolocardChangeBoundUserInterface _owner;

    public HolocardChangeWindow(HolocardChangeBoundUserInterface owner)
    {
        IoCManager.InjectDependencies(this);
        RobustXamlLoader.Load(this);

        _targetEntity = owner.Owner;
        _owner = owner;

        Title = Loc.GetString("ui-holocard-change-title");

        //ReagentList.OnItemSelected += ReagentListSelected;
        //ReagentList.OnItemDeselected += ReagentListDeselected;
        //SearchBar.OnTextChanged += (_) => UpdateReagentPrototypes(SearchBar.Text);
    }

}
