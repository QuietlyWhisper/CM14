using Content.Client._RMC14.Xenonids.UI;
using Content.Client.Administration.UI.CustomControls;
using Content.Shared._RMC14.Medical.Surgery;
using Content.Shared.Body.Part;
using Content.Shared.Rotation;
using Content.Shared.Standing;
using JetBrains.Annotations;
using Robust.Client.GameObjects;
using Robust.Client.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;
using static Robust.Client.UserInterface.Control;

namespace Content.Client._RMC14.Medical.Surgery;

[UsedImplicitly]
public sealed class CMSurgeryBui : BoundUserInterface
{
    [Dependency] private readonly IEntityManager _entities = default!;
    [Dependency] private readonly IPlayerManager _player = default!;

    private readonly CMSurgerySystem _system;

    [ViewVariables]
    private CMSurgeryWindow? _window;

    private EntityUid? _part;
    private (EntityUid Ent, EntProtoId Proto)? _surgery;
    private readonly List<EntProtoId> _previousSurgeries = new();

    public CMSurgeryBui(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
        _system = _entities.System<CMSurgerySystem>();
    }

    protected override void Open()
    {
        _system.OnRefresh += () =>
        {
            UpdateDisabledPanel();
            RefreshUI();
        };

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
            _window.Title = Loc.GetString("rmc-surgery-window-title");

            _window.PartsButton.OnPressed += _ =>
            {
                _part = null;
                _surgery = null;
                _previousSurgeries.Clear();
                View(ViewType.Parts);
            };

            _window.SurgeriesButton.OnPressed += _ =>
            {
                _surgery = null;
                _previousSurgeries.Clear();

                if (!_entities.TryGetNetEntity(_part, out var netPart) ||
                    State is not CMSurgeryBuiState s ||
                    !s.Choices.TryGetValue(netPart.Value, out var surgeries))
                {
                    return;
                }

                OnPartPressed(netPart.Value, surgeries);
            };

            _window.StepsButton.OnPressed += _ =>
            {
                if (!_entities.TryGetNetEntity(_part, out var netPart) ||
                    _previousSurgeries.Count == 0)
                {
                    return;
                }

                var last = _previousSurgeries[^1];
                _previousSurgeries.RemoveAt(_previousSurgeries.Count - 1);

                if (_system.GetSingleton(last) is not { } previousId ||
                    !_entities.TryGetComponent(previousId, out CMSurgeryComponent? previous))
                {
                    return;
                }

                OnSurgeryPressed((previousId, previous), netPart.Value, last);
            };
        }

        _window.Surgeries.DisposeAllChildren();
        _window.Steps.DisposeAllChildren();
        _window.Parts.DisposeAllChildren();

        View(ViewType.Parts);

        var oldSurgery = _surgery;
        var oldPart = _part;
        _part = null;
        _surgery = null;

        var parts = new List<Entity<BodyPartComponent>>(state.Choices.Keys.Count);
        foreach (var choice in state.Choices.Keys)
        {
            if (_entities.TryGetEntity(choice, out var ent) &&
                _entities.TryGetComponent(ent, out BodyPartComponent? part))
            {
                parts.Add((ent.Value, part));
            }
        }

        parts.Sort((a, b) =>
        {
            int GetScore(Entity<BodyPartComponent> part)
            {
                return part.Comp.PartType switch
                {
                    BodyPartType.Head => 1,
                    BodyPartType.Torso => 2,
                    BodyPartType.Arm => 3,
                    BodyPartType.Hand => 4,
                    BodyPartType.Leg => 5,
                    BodyPartType.Foot => 6,
                    BodyPartType.Tail => 7,
                    BodyPartType.Other => 8,
                    _ => 0
                };
            }

            return GetScore(a) - GetScore(b);
        });

        foreach (var part in parts)
        {
            var netPart = _entities.GetNetEntity(part.Owner);
            var surgeries = state.Choices[netPart];
            var partName = _entities.GetComponent<MetaDataComponent>(part).EntityName;
            var partButton = new XenoChoiceControl();

            partButton.Set(partName, null);
            partButton.Button.OnPressed += _ => OnPartPressed(netPart, surgeries);

            _window.Parts.AddChild(partButton);

            foreach (var surgeryId in surgeries)
            {
                if (_system.GetSingleton(surgeryId) is not { } surgery ||
                    !_entities.TryGetComponent(surgery, out CMSurgeryComponent? surgeryComp))
                {
                    continue;
                }

                if (oldPart == part && oldSurgery?.Proto == surgeryId)
                    OnSurgeryPressed((surgery, surgeryComp), netPart, surgeryId);
            }

            if (oldPart == part && oldSurgery == null)
                OnPartPressed(netPart, surgeries);
        }

        RefreshUI();
        UpdateDisabledPanel();

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

        var stepButton = new CMSurgeryStepButton { Step = step };
        stepButton.Button.OnPressed += _ => SendMessage(new CMSurgeryStepChosenBuiMsg(netPart, surgeryId, stepId));

        _window.Steps.AddChild(stepButton);
    }

    private void OnSurgeryPressed(Entity<CMSurgeryComponent> surgery, NetEntity netPart, EntProtoId surgeryId)
    {
        if (_window == null)
            return;

        _part = _entities.GetEntity(netPart);
        _surgery = (surgery, surgeryId);

        _window.Steps.DisposeAllChildren();

        if (surgery.Comp.Requirement is { } requirementId && _system.GetSingleton(requirementId) is { } requirement)
        {
            var label = new XenoChoiceControl();
            label.Button.OnPressed += _ =>
            {
                _previousSurgeries.Add(surgeryId);

                if (_entities.TryGetComponent(requirement, out CMSurgeryComponent? requirementComp))
                    OnSurgeryPressed((requirement, requirementComp), netPart, requirementId);
            };

            var msg = new FormattedMessage();
            var surgeryName = _entities.GetComponent<MetaDataComponent>(requirement).EntityName;
            msg.AddMarkupOrThrow(Loc.GetString("rmc-surgery-step-invalid-requires-surgery", ("surgery", surgeryName)));
            label.Set(msg, null);

            _window.Steps.AddChild(label);
            _window.Steps.AddChild(new HSeparator { Color = Color.FromHex("#4972A1"), Margin = new Thickness(0, 0, 0, 1) });
        }

        foreach (var stepId in surgery.Comp.Steps)
        {
            AddStep(stepId, netPart, surgeryId);
        }

        View(ViewType.Steps);
        RefreshUI();
    }

    private void OnPartPressed(NetEntity netPart, List<EntProtoId> surgeryIds)
    {
        if (_window == null)
            return;

        _part = _entities.GetEntity(netPart);

        _window.Surgeries.DisposeAllChildren();

        var surgeries = new List<(Entity<CMSurgeryComponent> Ent, EntProtoId Id, string Name)>();
        foreach (var surgeryId in surgeryIds)
        {
            if (_system.GetSingleton(surgeryId) is not { } surgery ||
                !_entities.TryGetComponent(surgery, out CMSurgeryComponent? surgeryComp))
            {
                continue;
            }

            var name = _entities.GetComponent<MetaDataComponent>(surgery).EntityName;
            surgeries.Add(((surgery, surgeryComp), surgeryId, name));
        }

        surgeries.Sort((a, b ) =>
        {
            var priority = a.Ent.Comp.Priority.CompareTo(b.Ent.Comp.Priority);
            if (priority != 0)
                return priority;

            return string.Compare(a.Name, b.Name, StringComparison.Ordinal);
        });

        foreach (var surgery in surgeries)
        {
            var surgeryButton = new XenoChoiceControl();
            surgeryButton.Set(surgery.Name, null);

            surgeryButton.Button.OnPressed += _ => OnSurgeryPressed(surgery.Ent, netPart, surgery.Id);
            _window.Surgeries.AddChild(surgeryButton);
        }

        RefreshUI();
        View(ViewType.Surgeries);
    }

    private void RefreshUI()
    {
        if (_window == null ||
            !_entities.HasComponent<CMSurgeryComponent>(_surgery?.Ent) ||
            !_entities.TryGetComponent(_part, out BodyPartComponent? part))
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
                    !_system.CanPerformStep(player, Owner, part.PartType, stepButton.Step, false, out var popup, out var reason, out _))
                {
                    stepButton.ToolTip = popup;
                    stepButton.Button.Disabled = true;

                    switch (reason)
                    {
                        case StepInvalidReason.MissingSkills:
                            stepName.AddMarkupOrThrow(Loc.GetString("rmc-surgery-step-invalid-missing-skill"));
                            break;
                        case StepInvalidReason.NeedsOperatingTable:
                            stepName.AddMarkupOrThrow(Loc.GetString("rmc-surgery-step-invalid-operating-table"));
                            break;
                        case StepInvalidReason.Armor:
                            stepName.AddMarkupOrThrow(Loc.GetString("rmc-surgery-step-invalid-remove-armor"));
                            break;
                        case StepInvalidReason.MissingTool:
                            stepName.AddMarkupOrThrow(Loc.GetString("rmc-surgery-step-invalid-missing-tool"));
                            break;
                    }
                }
            }

            var texture = _entities.GetComponentOrNull<SpriteComponent>(stepButton.Step)?.Icon?.Default;
            stepButton.Set(stepName, texture);
            i++;
        }

        UpdateDisabledPanel();
    }

    private void UpdateDisabledPanel()
    {
        if (_window == null)
            return;

        if (_system.IsLyingDown(Owner))
        {
            _window.DisabledPanel.Visible = false;
            _window.DisabledPanel.MouseFilter = MouseFilterMode.Ignore;
            return;
        }

        _window.DisabledPanel.Visible = true;

        var text = new FormattedMessage();
        text.AddMarkupOrThrow(Loc.GetString("rmc-surgery-invalid-need-lying-down"));
        _window.DisabledLabel.SetMessage(text);
        _window.DisabledPanel.MouseFilter = MouseFilterMode.Stop;
    }

    private void View(ViewType type)
    {
        if (_window == null)
            return;

        _window.PartsButton.Parent!.Margin = new Thickness(0, 0, 0, 10);

        _window.Parts.Visible = type == ViewType.Parts;
        _window.PartsButton.Disabled = type == ViewType.Parts;

        _window.Surgeries.Visible = type == ViewType.Surgeries;
        _window.SurgeriesButton.Disabled = type != ViewType.Steps;

        _window.Steps.Visible = type == ViewType.Steps;
        _window.StepsButton.Disabled = type != ViewType.Steps || _previousSurgeries.Count == 0;

        if (_entities.TryGetComponent(_part, out MetaDataComponent? partMeta) &&
            _entities.TryGetComponent(_surgery?.Ent, out MetaDataComponent? surgeryMeta))
        {
            _window.Title = Loc.GetString("rmc-surgery-part-surgery", ("part", partMeta.EntityName), ("surgery", surgeryMeta.EntityName));
        }
        else if (partMeta != null)
        {
            _window.Title = Loc.GetString("rmc-surgery-part", ("part", partMeta.EntityName));
        }
        else
        {
            _window.Title = Loc.GetString("rmc-surgery");
        }
    }

    private enum ViewType
    {
        Parts,
        Surgeries,
        Steps
    }

    private enum StepStatus
    {
        Next,
        Complete,
        Incomplete
    }
}
