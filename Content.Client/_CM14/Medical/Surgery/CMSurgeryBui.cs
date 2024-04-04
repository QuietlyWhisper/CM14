﻿using Content.Client._CM14.Xenos.UI;
using Content.Client.Administration.UI.CustomControls;
using Content.Shared._CM14.Medical.Surgery;
using JetBrains.Annotations;
using Robust.Client.GameObjects;
using Robust.Client.Player;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;
using static Robust.Client.UserInterface.Controls.Label;

namespace Content.Client._CM14.Medical.Surgery;

[UsedImplicitly]
public sealed class CMSurgeryBui : BoundUserInterface
{
    [Dependency] private readonly IEntityManager _entities = default!;
    [Dependency] private readonly IPlayerManager _player = default!;

    private readonly CMSurgerySystem _system;

    [ViewVariables]
    private CMSurgeryWindow? _window;

    private (EntityUid Ent, EntProtoId Proto)? _surgery;
    private EntityUid? _part;

    public CMSurgeryBui(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
        _system = _entities.System<CMSurgerySystem>();
    }

    protected override void Open()
    {
        _system.OnRefresh += RefreshUI;

        if (State is CMSurgeryBuiState s)
            Update(s);
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        if (state is CMSurgeryBuiState s)
            Update(s);
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);

        if (disposing)
            _window?.Dispose();

        _system.OnRefresh -= RefreshUI;
    }

    private void Update(CMSurgeryBuiState state)
    {
        if (_window == null)
        {
            _window = new CMSurgeryWindow();
            _window.OnClose += Close;
            _window.Title = "Surgery";

            var surgeries = new FormattedMessage();
            surgeries.AddMarkup("[bold]Surgeries[/bold]");
            _window.SurgeriesLabel.SetMessage(surgeries);

            var parts = new FormattedMessage();
            parts.AddMarkup("[bold]Parts[/bold]");
            _window.PartsLabel.SetMessage(parts);

            var steps = new FormattedMessage();
            steps.AddMarkup("[bold]Steps[/bold]");
            _window.StepsLabel.SetMessage(steps);
        }

        _window.Surgeries.DisposeAllChildren();
        _window.Steps.DisposeAllChildren();
        _window.Parts.DisposeAllChildren();

        var oldSurgery = _surgery;
        var oldPart = _part;
        _surgery = null;
        _part = null;

        foreach (var (surgeryId, parts) in state.Choices)
        {
            var surgeryEnt = _system.GetSingleton(surgeryId);
            if (!_entities.TryGetComponent(surgeryEnt, out CMSurgeryComponent? surgery))
                continue;

            var name = _entities.GetComponent<MetaDataComponent>(surgeryEnt.Value).EntityName;
            var surgeryButton = new Button
            {
                Text = name,
                StyleClasses = { "OpenBoth" },
                TextAlign = AlignMode.Left
            };

            surgeryButton.OnPressed += _ => OnSurgeryPressed((surgeryEnt.Value, surgery), parts, surgeryId);
            _window.Surgeries.AddChild(surgeryButton);

            if (oldSurgery?.Proto == surgeryId)
            {
                var select = oldPart == null ? null : _entities.GetNetEntity(oldPart);
                OnSurgeryPressed((surgeryEnt.Value, surgery), parts, surgeryId, select);
            }
        }

        RefreshUI();

        if (!_window.IsOpen)
            _window.OpenCentered();
    }

    private void AddStep(EntProtoId stepId, NetEntity netPart, EntProtoId surgeryId)
    {
        if (_window == null ||
            _system.GetSingleton(stepId) is not { } step)
        {
            return;
        }

        var stepName = new FormattedMessage();
        stepName.AddText(_entities.GetComponent<MetaDataComponent>(step).EntityName);

        // TODO cm14 rename this control
        var stepButton = new CMSurgeryStepButton { Step = step };
        stepButton.Button.OnPressed += _ => SendMessage(new CMSurgeryStepChosenBuiMessage(netPart, surgeryId, stepId));

        _window.Steps.AddChild(stepButton);
    }

    private void OnSurgeryPressed(Entity<CMSurgeryComponent> surgery, List<NetEntity> parts, EntProtoId surgeryId, NetEntity? select = null)
    {
        if (_window == null)
            return;

        _surgery = (surgery, surgeryId);
        _part = select == null ? null : _entities.GetEntity(select);

        _window.Steps.DisposeAllChildren();
        _window.Parts.DisposeAllChildren();

        foreach (var netPart in parts)
        {
            var part = _entities.GetEntity(netPart);
            var partName = _entities.GetComponent<MetaDataComponent>(part).EntityName;
            var partButton = new Button
            {
                Text = partName,
                StyleClasses = { "OpenBoth" },
                TextAlign = AlignMode.Left
            };

            partButton.OnPressed += _ => OnPartPressed(surgery, parts, surgeryId, netPart);

            _window.Parts.AddChild(partButton);

            if (select == netPart)
                OnPartPressed(surgery, parts, surgeryId, netPart);
        }

        RefreshUI();
    }

    private void OnPartPressed(Entity<CMSurgeryComponent> surgery, List<NetEntity> parts, EntProtoId surgeryId, NetEntity netPart)
    {
        if (_window == null)
            return;

        _part = _entities.GetEntity(netPart);

        _window.Steps.DisposeAllChildren();

        if (surgery.Comp.Requirement is { } requirementId && _system.GetSingleton(requirementId) is { } requirement)
        {
            var label = new XenoChoiceControl();
            label.Button.OnPressed += _ =>
            {
                if (_entities.TryGetComponent(requirement, out CMSurgeryComponent? requirementComp))
                    OnSurgeryPressed((requirement, requirementComp), parts, requirementId, netPart);
            };

            var msg = new FormattedMessage();
            var surgeryName = _entities.GetComponent<MetaDataComponent>(requirement).EntityName;
            msg.AddMarkup($"[bold]Requires: {surgeryName}[/bold]");
            label.Set(msg, null);

            _window.Steps.AddChild(label);
            _window.Steps.AddChild(new HSeparator { Color = Color.FromHex("#4972A1"), Margin = new Thickness(0, 0, 0, 1) });
        }

        foreach (var stepId in surgery.Comp.Steps)
        {
            AddStep(stepId, netPart, surgeryId);
        }

        RefreshUI();
    }

    private void RefreshUI()
    {
        if (_window == null ||
            !_entities.TryGetComponent(_surgery?.Ent, out CMSurgeryComponent? surgery) ||
            _part == null)
        {
            return;
        }

        var next = _system.GetNextStep(Owner, _part.Value, _surgery.Value.Ent);
        var i = 0;
        foreach (var child in _window.Steps.Children)
        {
            if (child is not CMSurgeryStepButton stepButton)
                continue;

            var status = StepStatus.Incomplete;
            if (next == null)
            {
                status = StepStatus.Complete;
            }
            else if (next.Value.Surgery.Owner != _surgery.Value.Ent)
            {
                status = StepStatus.Incomplete;
            }
            else if (next.Value.Step == i)
            {
                status = StepStatus.Next;
            }
            else if (i < next.Value.Step)
            {
                status = StepStatus.Complete;
            }

            stepButton.Button.Disabled = status != StepStatus.Next;

            var stepName = new FormattedMessage();
            stepName.AddText(_entities.GetComponent<MetaDataComponent>(stepButton.Step).EntityName);

            if (status == StepStatus.Complete)
            {
                stepButton.Button.Modulate = Color.Green;
            }
            else
            {
                stepButton.Button.Modulate = Color.White;
                if (_player.LocalEntity is { } player &&
                    !_system.CanPerformStep(player, stepButton.Step, false, out var popup))
                {
                    stepButton.ToolTip = popup;
                    stepButton.Button.Disabled = true;
                    stepName.AddMarkup(" [color=red](Missing tool)[/color]");
                }
            }

            var texture = _entities.GetComponentOrNull<SpriteComponent>(stepButton.Step)?.Icon?.Default;
            stepButton.Set(stepName, texture);
            i++;
        }
    }

    private enum StepStatus
    {
        Next,
        Complete,
        Incomplete
    }
}
