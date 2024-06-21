using Content.Server.Chat.Systems;
using Content.Shared._CM14.Xenos.Hugger;

namespace Content.Server._CM14.Xenos;

public sealed class XenoHuggerEmoteSystem : EntitySystem
{
    [Dependency] private readonly ChatSystem _chat = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<VictimHuggedComponent, VictimHuggedEmoteEvent>(OnDoEmote);
    }

    private void OnDoEmote(Entity<VictimHuggedComponent> ent, ref VictimHuggedEmoteEvent args)
    {
        _chat.TryEmoteWithChat(ent, args.Emote);
    }
}
