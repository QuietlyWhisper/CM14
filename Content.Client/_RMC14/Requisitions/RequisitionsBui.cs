using Content.Shared._RMC14.Requisitions;
using Content.Shared._RMC14.Requisitions.Components;
using JetBrains.Annotations;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;
using Robust.Shared.Log;
using static Content.Shared._RMC14.Requisitions.Components.RequisitionsElevatorMode;

namespace Content.Client._RMC14.Requisitions;

[UsedImplicitly]
public sealed class RequisitionsBui : BoundUserInterface
{
    [Dependency] private readonly IEntityManager _entities = default!;
    [Dependency] private readonly IPrototypeManager _prototypes = default!;

    [ViewVariables]
    private RequisitionsWindow? _window;

    public RequisitionsBui(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
    }

    protected override void Open()
    {
        _window = new RequisitionsWindow();
        _window.OnClose += Close;

        _window.MainView.OrderItemsButton.OnPressed += _ => ShowView(_window, _window.OrderCategoriesView);
        _window.MainView.ViewRequestsButton.OnPressed += _ => { };
        _window.MainView.ViewOrdersButton.OnPressed += _ => { };

        _window.OrderCategoriesView.MainMenuButton.OnPressed += _ => ShowView(_window, _window.MainView);
        _window.OrderCategoriesView.SearchMenuButton.OnPressed += _ => ShowView(_window, _window.OrderSearchView);

        _window.OrderSearchView.BackButton.OnPressed += _ => ShowView(_window, _window.OrderCategoriesView);
        _window.OrderSearchView.SearchBar.OnTextChanged += _ => {
            UpdateItemSearch(_window.OrderSearchView.SearchBar.Text);
        };

        _window.CategoryView.BackButton.OnPressed += _ => ShowView(_window, _window.OrderCategoriesView);

        _window.OpenCentered();
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        if (state is RequisitionsBuiState uiState)
            UpdateState(uiState);
    }

    private void UpdateState(RequisitionsBuiState uiState)
    {
        if (_window == null)
        {
            _window = new RequisitionsWindow();
            _window.OnClose += Close;
        }

        var platformLabel = Loc.GetString("rmc-requisitions-no-platform");
        var platformButtonLabel = Loc.GetString("rmc-requisitions-no-platform");
        var platformButtonDisabled = false;
        bool? raise = null;
        switch (uiState.PlatformLowered)
        {
            case Lowered or Raised when uiState.Busy:
                platformLabel = Loc.GetString("rmc-requisitions-platform-position", ("position", uiState.PlatformLowered));
                platformButtonLabel = Loc.GetString("rmc-requisitions-platform-asrs-busy");
                platformButtonDisabled = true;
                break;
            case Lowered:
                platformButtonLabel = Loc.GetString("rmc-requisitions-platform-raise");
                platformLabel = Loc.GetString("rmc-requisitions-platform-lowered");
                raise = true;
                break;
            case Raised:
                platformButtonLabel = Loc.GetString("rmc-requisitions-platform-lower");
                platformLabel = Loc.GetString("rmc-requisitions-platform-raised");
                raise = false;
                break;
            case Lowering:
                platformButtonLabel = Loc.GetString("rmc-requisitions-platform-please-wait");
                platformLabel = Loc.GetString("rmc-requisitions-platform-lowering");
                platformButtonDisabled = true;
                break;
            case Raising:
                platformButtonLabel = Loc.GetString("rmc-requisitions-platform-please-wait");
                platformLabel = Loc.GetString("rmc-requisitions-platform-raising");
                platformButtonDisabled = true;
                break;
            case null:
                platformButtonDisabled = true;
                break;
        }

        _window.MainView.PlatformLabel.SetMessage(platformLabel);
        _window.MainView.PlatformButton.Text = platformButtonLabel;

        _window.MainView.PlatformButton.Disabled = platformButtonDisabled;

        if (raise != null)
        {
            _window.MainView.PlatformButton.OnPressed += _ => SendMessage(new RequisitionsPlatformMsg(raise.Value));
        }

        var budget = new FormattedMessage();
        budget.AddMarkupOrThrow(Loc.GetString("rmc-requisitions-budget", ("balance", uiState.Balance)));
        _window.MainView.BudgetLabel.SetMessage(budget);
        _window.OrderCategoriesView.BudgetLabel.SetMessage(budget);
        _window.CategoryView.BudgetLabel.SetMessage(budget);
        _window.OrderSearchView.BudgetLabel.SetMessage(budget);

        var categoryHeader = new FormattedMessage();
        categoryHeader.AddMarkupOrThrow(Loc.GetString("rmc-requisitions-select-category"));
        _window.OrderCategoriesView.CategoryHeaderLabel.SetMessage(categoryHeader);
        _window.OrderCategoriesView.CategoriesContainer.DisposeAllChildren();

        if (_entities.TryGetComponent(Owner, out RequisitionsComputerComponent? computer))
        {
            for (var i = 0; i < computer.Categories.Count; i++)
            {
                var category = computer.Categories[i];
                var uiCategory = new Button { Text = category.Name, StyleClasses = { "ButtonSquare" } };
                var categoryIndex = i;
                uiCategory.OnPressed += _ => ChangeOrderCategory(categoryIndex);
                _window.OrderCategoriesView.CategoriesContainer.AddChild(uiCategory);
            }
        }

        foreach (var child in _window.CategoryView.OrdersContainer.Children)
        {
            if (child is RequisitionsOrderButton order)
                UpdateOrderButton(order, uiState);
        }

        foreach (var group in _window.OrderSearchView.ResultContainer.Children)
        {
            if (group is RequisitionsOrderSearchGroup categoryGroup)
            {
                foreach (var child in categoryGroup.GroupItems.Children)
                {
                    if (child is RequisitionsOrderButton order)
                        UpdateOrderButton(order, uiState);
                }
            }
        }

        if (!_window.IsOpen)
            _window.OpenCentered();
    }

    private void ShowView(RequisitionsWindow window, Control view)
    {
        foreach (var child in window.Contents.Children)
        {
            child.Visible = child == view;
        }
    }

    private void ChangeOrderCategory(int categoryIndex)
    {
        if (_window == null)
            return;

        if (!_entities.TryGetComponent(Owner, out RequisitionsComputerComponent? computer) ||
            categoryIndex >= computer.Categories.Count)
        {
            return;
        }

        ShowView(_window, _window.CategoryView);
        _window.CategoryView.OrdersContainer.DisposeAllChildren();

        var category = computer.Categories[categoryIndex];
        var requestMsg = new FormattedMessage();
        requestMsg.AddMarkupOrThrow(Loc.GetString("rmc-requisitions-request-from", ("category", category.Name)));
        _window.CategoryView.RequestFromLabel.SetMessage(requestMsg);

        var state = State as RequisitionsBuiState;
        for (var i = 0; i < category.Entries.Count; i++)
        {
            var entry = category.Entries[i];
            var order = new RequisitionsOrderButton();
            var orderIndex = i;
            order.Button.Text = entry.Name ?? _prototypes.Index<EntityPrototype>(entry.Crate).Name;
            order.Button.OnPressed += _ => SendMessage(new RequisitionsBuyMsg(categoryIndex, orderIndex));

            order.SetCost(entry.Cost);
            UpdateOrderButton(order, state);
            _window.CategoryView.OrdersContainer.AddChild(order);
        }
    }

    private void UpdateItemSearch(string? filter = null)
    {
        if (_window == null)
            return;

        if (!_entities.TryGetComponent(Owner, out RequisitionsComputerComponent? computer))
            return;

        _window.OrderSearchView.ResultContainer.DisposeAllChildren();

        if (string.IsNullOrEmpty(filter))
            return;

        var state = State as RequisitionsBuiState;
        for (int catIndex = 0; catIndex < computer.Categories.Count; catIndex++)
        {
            var entryCount = 0;
            var category = computer.Categories[catIndex];
            var categoryGroup = new RequisitionsOrderSearchGroup();

            for (var entryIndex = 0; entryIndex < category.Entries.Count; entryIndex++)
            {
                var entry = category.Entries[entryIndex];
                var itemName = entry.Name ?? _prototypes.Index<EntityPrototype>(entry.Crate).Name;

                if (!itemName.ToLowerInvariant().Contains(filter.Trim().ToLowerInvariant()))
                {
                    continue;
                }

                var order = new RequisitionsOrderButton();
                var orderIndex = entryIndex;
                var categoryIndex = catIndex;
                order.Button.Text = itemName;
                order.Button.OnPressed += _ => SendMessage(new RequisitionsBuyMsg(categoryIndex, orderIndex));

                order.SetCost(entry.Cost);
                UpdateOrderButton(order, state);
                categoryGroup.GroupItems.AddChild(order);
                entryCount++;
            }

            if (entryCount < 1)
                continue;

            var categoryHeader = new FormattedMessage();
            categoryHeader.AddMarkupOrThrow(Loc.GetString("rmc-requisitions-request-from", ("category", category.Name)));
            categoryGroup.GroupLabel.SetMessage(categoryHeader);

            _window.OrderSearchView.ResultContainer.AddChild(categoryGroup);
        }
    }

    private void UpdateOrderButton(RequisitionsOrderButton order, RequisitionsBuiState? state)
    {
        order.Button.Disabled = state == null ||
                                state.Balance < order.Cost ||
                                state.Full;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
            _window?.Dispose();
    }
}
