using Content.Shared.Actions;
using Content.Shared.Examine;

namespace Content.Shared._RMC14.Examine.Pose;

public abstract class SharedRMCSetPoseSystem : EntitySystem
{
    [Dependency] private readonly SharedActionsSystem _actions = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<RMCSetPoseComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<RMCSetPoseComponent, ExaminedEvent>(OnExamine);
    }

    private void OnMapInit(Entity<RMCSetPoseComponent> ent, ref MapInitEvent ev)
    {
        _actions.AddAction(ent, ref ent.Comp.Action, ent.Comp.ActionPrototype);
    }

    private void OnExamine(Entity<RMCSetPoseComponent> ent, ref ExaminedEvent args)
    {
        var comp = ent.Comp;

        if (comp.Pose.Trim() == string.Empty)
            return;

        using (args.PushGroup(nameof(RMCSetPoseComponent)))
        {
            args.PushMarkup(string.Empty, -1);

            var pose = Loc.GetString("rmc-set-pose-examined", ("ent", ent), ("pose", comp.Pose));
            args.PushMarkup(pose, -2);
        }
    }
}

public sealed partial class RMCSetPoseActionEvent : InstantActionEvent;
