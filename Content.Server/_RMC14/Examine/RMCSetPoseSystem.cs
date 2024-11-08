using Content.Server.Administration;
using Content.Shared._RMC14.Examine.Pose;
using Content.Shared.Actions;
using Content.Shared.Verbs;
using Robust.Shared.Player;
using Robust.Shared.Utility;

namespace Content.Server._RMC14.Examine;

public sealed class RMCSetPoseSystem : SharedRMCSetPoseSystem
{
    [Dependency] private readonly QuickDialogSystem _quickDialog = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<RMCSetPoseComponent, GetVerbsEvent<Verb>>(OnSetPoseGetVerbs);
    }

    private void OnSetPoseGetVerbs(Entity<RMCSetPoseComponent> ent, ref GetVerbsEvent<Verb> args)
    {
        if (args.User != args.Target)
            return;

        if (!TryComp<ActorComponent>(ent, out var actor))
            return;

        var setPosePrompt = Loc.GetString("rmc-set-pose-dialog", ("ent", ent));

        Verb verb = new()
        {
            Text = Loc.GetString("pointing-verb-get-data-text"),
            Icon = new SpriteSpecifier.Texture(new("/Textures/Interface/character.svg.192dpi.png")),
            ClientExclusive = true,
            Act = () => _quickDialog.OpenDialog(actor.PlayerSession, Loc.GetString("rmc-set-pose-title"), setPosePrompt,
            (string pose) =>
            {
                ent.Comp.Pose = pose;
                Dirty(ent);
            })
        };

        args.Verbs.Add(verb);
    }
}
