using Content.Shared._RMC14.Areas;
using Content.Shared._RMC14.Roles.FindParasite;
using Content.Shared._RMC14.Xenonids.Construction.EggMorpher;
using Content.Shared._RMC14.Xenonids.Egg;
using Content.Shared._RMC14.Xenonids.Projectile.Parasite;
using Content.Shared.Coordinates;
using Content.Shared.Ghost;
using Content.Shared.Verbs;
using Robust.Shared.Console;
using Robust.Shared.Network;
using Robust.Shared.Player;

namespace Content.Server._RMC14.Roles;

public sealed partial class FindParasiteSystem : EntitySystem
{
    [Dependency] private readonly IConsoleHost _host = default!;
    [Dependency] private readonly INetManager _net = default!;
    [Dependency] private readonly EntityManager _entities = default!;
    [Dependency] private readonly SharedUserInterfaceSystem _ui = default!;
    [Dependency] private readonly AreaSystem _areas = default!;
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<FindParasiteComponent, FindParasiteActionEvent>(FindParasites);

        SubscribeLocalEvent<FindParasiteComponent, BoundUIOpenedEvent>(GetAllActiveParasiteSpawners);
        SubscribeLocalEvent<FindParasiteComponent, FollowParasiteSpawnerMessage>(FollowParasiteSpawner);
        SubscribeLocalEvent<FindParasiteComponent, TakeParasiteRoleMessage>(TakeParasiteRole);
    }

    private void FindParasites(Entity<FindParasiteComponent> parasiteFinderEnt, ref FindParasiteActionEvent args)
    {
        if (args.Handled)
        {
            return;
        }
        var ent = args.Performer;

        _ui.OpenUi(parasiteFinderEnt.Owner, XenoFindParasiteUI.Key, parasiteFinderEnt);
        args.Handled = true;
    }

    private void GetAllActiveParasiteSpawners(Entity<FindParasiteComponent> parasiteFinderEnt, ref BoundUIOpenedEvent args)
    {
        var ent = parasiteFinderEnt.Owner;
        var uiState = new FindParasiteUIState();

        var eggs = EntityQueryEnumerator<XenoEggComponent>();
        var eggMorphers = EntityQueryEnumerator<EggMorpherComponent>();
        var parasiteThrowers = EntityQueryEnumerator<XenoParasiteThrowerComponent>();

        var spawners = new List<NetEntity>();
        while (eggs.MoveNext(out var eggEnt, out var egg))
        {
            if (egg.State != XenoEggState.Grown)
            {
                continue;
            }

            var netEnt = _entities.GetNetEntity(eggEnt);
            spawners.Add(netEnt);
        }

        while (eggMorphers.MoveNext(out var eggMorpherEnt, out var eggMorpherComp))
        {
            if (eggMorpherComp.CurParasites <= eggMorpherComp.ReservedParasites ||
                eggMorpherComp.CurParasites == 0)
            {
                continue;
            }
            spawners.Add(_entities.GetNetEntity(eggMorpherEnt));
        }

        while (parasiteThrowers.MoveNext(out var throwerEnt, out var parasiteThrower))
        {
            if (parasiteThrower.CurParasites <= parasiteThrower.ReservedParasites ||
                parasiteThrower.CurParasites == 0)
            {
                continue;
            }
            spawners.Add(_entities.GetNetEntity(throwerEnt));
        }

        foreach (var spawner in spawners)
        {
            var spawnerEnt = _entities.GetEntity(spawner);
            var name = MetaData(spawnerEnt).EntityName;
            var areaName = Loc.GetString("xeno-ui-default-area-name");
            if (_areas.TryGetArea(spawnerEnt.ToCoordinates(), out AreaComponent? area, out _, out var areaEnt) &&
                areaEnt is EntityUid)
            {
                areaName = MetaData(areaEnt.Value).EntityName;
            }
            name = Loc.GetString("xeno-ui-find-parasite-item",
                    ("itemName", name), ("areaName", areaName));

            uiState.ActiveParasiteSpawners.Add(new(name, spawner));
        }
        _ui.SetUiState(ent, XenoFindParasiteUI.Key, uiState);

    }
    private void FollowParasiteSpawner(Entity<FindParasiteComponent> parasiteFinderEnt, ref FollowParasiteSpawnerMessage args)
    {
        var netEnt = args.Entity;
        var ent = _entities.GetEntity(netEnt);

        var netSpawner = args.Spawner;
        var spawner = _entities.GetEntity(netSpawner);

        var ev = new GetVerbsEvent<AlternativeVerb>(ent, spawner, null, null, true, true, true, new());
        RaiseLocalEvent(ev);

        foreach (var action in ev.Verbs)
        {
            if (action.Text != Loc.GetString("verb-follow-text") || action.Act is null)
            {
                continue;
            }
            action.Act.Invoke();
            break;
        }
    }

    private void TakeParasiteRole(Entity<FindParasiteComponent> parasiteFinderEnt, ref TakeParasiteRoleMessage args)
    {
        var netEnt = args.Entity;
        var ent = _entities.GetEntity(netEnt);

        var netSpawner = args.Spawner;
        var spawner = _entities.GetEntity(netSpawner);

        var ev = new GetVerbsEvent<ActivationVerb>(ent, spawner, null, null, false, false, false, new());
        RaiseLocalEvent(spawner, ev);

        foreach (var action in ev.Verbs)
        {
            if (action.Text != Loc.GetString("rmc-xeno-egg-ghost-verb") || action.Act is null)
            {
                continue;
            }
            action.Act.Invoke();
            break;
        }
    }
}
