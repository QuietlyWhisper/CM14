using System.Linq;
using Content.Shared._RMC14.Holiday;
using Content.Shared._RMC14.Medical.Refill;
using Content.Shared._RMC14.Vendors;
using Content.Shared.Mind;
using Content.Shared.Roles.Jobs;
using JetBrains.Annotations;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Client.ResourceManagement;
using Robust.Client.UserInterface.CustomControls;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;
using static System.StringComparison;
using static Robust.Client.UserInterface.Controls.LineEdit;

namespace Content.Client._RMC14.Vendors;

[UsedImplicitly]
public sealed class CMAutomatedVendorBui : BoundUserInterface
{
    [Dependency] private readonly IPlayerManager _player = default!;
    [Dependency] private readonly IPrototypeManager _prototype = default!;
    [Dependency] private readonly IResourceCache _resource = default!;

    private readonly SharedJobSystem _job;
    private readonly SharedMindSystem _mind;
    private readonly SharedRMCHolidaySystem _rmcHoliday;

    private CMAutomatedVendorWindow? _window;

    public CMAutomatedVendorBui(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
        _job = EntMan.System<SharedJobSystem>();
        _mind = EntMan.System<SharedMindSystem>();
        _rmcHoliday = EntMan.System<SharedRMCHolidaySystem>();
    }

    protected override void Open()
    {
        _window = new CMAutomatedVendorWindow();
        _window.OnClose += Close;
        _window.Title = EntMan.GetComponentOrNull<MetaDataComponent>(Owner)?.EntityName ?? Loc.GetString("rmc-automated-vendor-colmartech-vendor");
        _window.ReagentsBar.ForegroundStyleBoxOverride = new StyleBoxFlat(Color.FromHex("#AF7F38"));

        var user = EntMan.GetComponentOrNull<CMVendorUserComponent>(_player.LocalEntity);
        if (EntMan.TryGetComponent(Owner, out CMAutomatedVendorComponent? vendor))
        {
            for (var sectionIndex = 0; sectionIndex < vendor.Sections.Count; sectionIndex++)
            {
                var section = vendor.Sections[sectionIndex];

                var validJob = true;
                if (_player.LocalSession != null && _mind.TryGetMind(_player.LocalSession.UserId, out var mindId))
                {
                    foreach (var job in section.Jobs)
                    {
                        if (!_job.MindHasJobWithId(mindId, job.Id))
                            validJob = false;
                        else
                            validJob = true;
                    }
                }

                var validHoliday = section.Holidays.Count == 0;
                foreach (var holiday in section.Holidays)
                {
                    if (_rmcHoliday.IsActiveHoliday(holiday))
                        validHoliday = true;
                }

                var uiSection = new CMAutomatedVendorSection();
                uiSection.Label.SetMessage(GetSectionName(user, section));

                if (!validJob || !validHoliday)
                    uiSection.Visible = false; // hide the section

                for (var entryIndex = 0; entryIndex < section.Entries.Count; entryIndex++)
                {
                    var entry = section.Entries[entryIndex];
                    var uiEntry = new CMAutomatedVendorEntry();

                    if (_prototype.TryIndex(entry.Id, out var entity))
                    {
                        uiEntry.Texture.Textures = SpriteComponent.GetPrototypeTextures(entity, _resource)
                            .Select(o => o.Default)
                            .ToList();
                        uiEntry.Panel.Button.Label.Text = entry.Name?.Replace("\\n", "\n") ?? entity.Name;

                        var name = entity.Name;
                        var color = CMAutomatedVendorPanel.DefaultColor;
                        var borderColor = CMAutomatedVendorPanel.DefaultBorderColor;
                        var hoverColor = CMAutomatedVendorPanel.DefaultBorderColor;
                        if (section.TakeAll != null)
                        {
                            name = Loc.GetString("rmc-automated-vendor-mandatory", ("name", name));
                            color = Color.FromHex("#251A0C");
                            borderColor = Color.FromHex("#805300");
                            hoverColor = Color.FromHex("#805300");
                        }
                        else if (entry.Recommended)
                        {
                            name = Loc.GetString("rmc-automated-vendor-recommended", ("name", name));
                            color = Color.FromHex("#102919");
                            borderColor = Color.FromHex("#3A9B52");
                            hoverColor = Color.FromHex("#3A9B52");
                        }

                        uiEntry.Panel.Color = color;
                        uiEntry.Panel.BorderColor = borderColor;
                        uiEntry.Panel.HoveredColor = hoverColor;

                        var msg = new FormattedMessage();
                        msg.AddText(name);
                        msg.PushNewline();

                        if (!string.IsNullOrWhiteSpace(entity.Description))
                            msg.AddText(entity.Description);

                        var tooltip = new Tooltip();
                        tooltip.SetMessage(msg);

                        uiEntry.TooltipLabel.ToolTip = entity.Description;
                        uiEntry.TooltipLabel.TooltipDelay = 0;
                        uiEntry.TooltipLabel.TooltipSupplier = _ => tooltip;

                        var sectionI = sectionIndex;
                        var entryI = entryIndex;
                        uiEntry.Panel.Button.OnPressed += _ => OnButtonPressed(sectionI, entryI);
                    }

                    uiSection.Entries.AddChild(uiEntry);
                }

                _window.Sections.AddChild(uiSection);
            }
        }

        _window.Search.OnTextChanged += OnSearchChanged;

        Refresh();

        _window.OpenCentered();
    }

    private void OnButtonPressed(int sectionIndex, int entryIndex)
    {
        var msg = new CMVendorVendBuiMsg(sectionIndex, entryIndex);
        SendMessage(msg);
    }

    private void OnSearchChanged(LineEditEventArgs args)
    {
        if (_window == null)
            return;

        foreach (var sectionControl in _window.Sections.Children)
        {
            if (sectionControl is not CMAutomatedVendorSection section)
                continue;

            var any = false;
            foreach (var entriesControl in section.Entries.Children)
            {
                if (entriesControl is not CMAutomatedVendorEntry entry)
                    continue;

                if (string.IsNullOrWhiteSpace(args.Text))
                    entry.Visible = true;
                else
                    entry.Visible = entry.Panel.Button.Label.Text?.Contains(args.Text, OrdinalIgnoreCase) ?? false;

                if (entry.Visible)
                    any = true;
            }

            section.Visible = any;
        }
    }

    public void Refresh()
    {
        if (_window == null)
            return;

        if (!EntMan.TryGetComponent(Owner, out CMAutomatedVendorComponent? vendor))
            return;

        var anyEntryWithPoints = false;
        var user = EntMan.GetComponentOrNull<CMVendorUserComponent>(_player.LocalEntity);
        var userPoints = vendor.PointsType == null
            ? user?.Points ?? 0
            : user?.ExtraPoints?.GetValueOrDefault(vendor.PointsType) ?? 0;
        for (var sectionIndex = 0; sectionIndex < vendor.Sections.Count; sectionIndex++)
        {
            var section = vendor.Sections[sectionIndex];
            var uiSection = (CMAutomatedVendorSection) _window.Sections.GetChild(sectionIndex);
            uiSection.Label.SetMessage(GetSectionName(user, section));

            var sectionDisabled = false;
            if (section.Choices is { } choices)
            {
                if (user?.Choices.GetValueOrDefault(choices.Id) >= choices.Amount ||
                    user == null && choices.Amount <= 0)
                {
                    sectionDisabled = true;
                }
            }

            var anyAmount = false;
            for (var entryIndex = 0; entryIndex < section.Entries.Count; entryIndex++)
            {
                var entry = section.Entries[entryIndex];
                var uiEntry = (CMAutomatedVendorEntry) uiSection.Entries.GetChild(entryIndex);
                var disabled = sectionDisabled || entry.Amount <= 0;
                if (section.TakeAll is { } takeAllId)
                {
                    var takeAll = user?.TakeAll;
                    if (takeAll != null && takeAll.Contains((takeAllId, entry.Id)))
                        disabled = true;
                }

                if (entry.Points != null)
                {
                    anyEntryWithPoints = true;
                    uiEntry.Amount.Text = $"{entry.Points}P";

                    if (user == null || userPoints < entry.Points)
                    {
                        disabled = true;
                    }
                }
                else
                {
                    uiEntry.Amount.Text = entry.Amount.ToString();
                }

                uiEntry.Amount.Modulate = disabled ? Color.Red : Color.White;
                uiEntry.Panel.Button.Disabled = disabled;

                if (!string.IsNullOrWhiteSpace(uiEntry.Amount.Text))
                    anyAmount = true;
            }

            for (var entryIndex = 0; entryIndex < section.Entries.Count; entryIndex++)
            {
                var uiEntry = (CMAutomatedVendorEntry) uiSection.Entries.GetChild(entryIndex);
                uiEntry.Amount.Visible = anyAmount;
            }
        }

        _window.PointsLabel.Text = anyEntryWithPoints ? $"Points Remaining: {userPoints}" : string.Empty;

        if (!EntMan.TryGetComponent(Owner, out CMSolutionRefillerComponent? refiller))
        {
            _window.ReagentsContainer.Visible = false;
            return;
        }

        _window.ReagentsContainer.Visible = true;

        var current = refiller.Current;
        var max = refiller.Max;
        _window.ReagentsBar.MinValue = 0;
        _window.ReagentsBar.MaxValue = max.Int();
        _window.ReagentsBar.SetAsRatio((refiller.Current / refiller.Max).Float());
        _window.ReagentsLabel.Text = Loc.GetString("rmc-automated-vendor-units", ("units", current.Int()));
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
            _window?.Dispose();
    }

    protected override void ReceiveMessage(BoundUserInterfaceMessage message)
    {
        switch (message)
        {
            case CMVendorRefreshBuiMsg:
                Refresh();
                break;
        }
    }

    private FormattedMessage GetSectionName(CMVendorUserComponent? user, CMVendorSection section)
    {
        var name = new FormattedMessage();
        name.PushTag(new MarkupNode("bold", new MarkupParameter(section.Name.ToUpperInvariant()), null));
        name.AddText(section.Name.ToUpperInvariant());

        if (section.TakeAll != null)
        {
            var takeAll = user?.TakeAll;
            foreach (var entry in section.Entries)
            {
                if (takeAll == null || !takeAll.Contains((section.TakeAll, entry.Id)))
                {
                    name.AddText(Loc.GetString("rmc-automated-vendor-take-all-suffix"));
                    break;
                }
            }
        }

        else if (section.Choices is { } choices)
        {
            if (user == null)
            {
                name.AddText(Loc.GetString("rmc-automated-vendor-choose-suffix", ("amount", choices.Amount)));
            }
            else
            {
                var left = choices.Amount - user.Choices.GetValueOrDefault(choices.Id);
                if (left > 0)
                    name.AddText(Loc.GetString("rmc-automated-vendor-choose-remaining-suffix", ("remaining", left)));
            }
        }

        name.Pop();
        return name;
    }
}
