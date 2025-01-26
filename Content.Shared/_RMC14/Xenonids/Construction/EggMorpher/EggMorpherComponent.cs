using Robust.Shared.GameStates;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Content.Shared._RMC14.Xenonids.Construction.EggMorpher;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class EggMorpherComponent : Component
{
    public const string ParasitePrototype = "CMXenoParasite";

    /// <summary>
    /// Currently stored parasites
    /// </summary>
    [DataField, AutoNetworkedField]
    public int CurParasites = 0;

    /// <summary>
    /// Max stored parasites
    /// </summary>
    [DataField, AutoNetworkedField]
    public int MaxParasites = 12;

    /// <summary>
    /// Max parasites that can be grown passively within the egg morpher
    /// </summary>
    [DataField, AutoNetworkedField]
    public int GrowMaxParasites = 6;

    [DataField, AutoNetworkedField]
    public int ReservedParasites = 0;

    /// <summary>
    /// How long it takes to spawn a single parasite
    /// </summary>
    [DataField, AutoNetworkedField]
    public TimeSpan StandardSpawnCooldown = TimeSpan.FromSeconds(120);

    /// <summary>
    /// How long it takes to spawn a single parasite while the queen is oving
    /// </summary>
    [DataField, AutoNetworkedField]
    public TimeSpan OviSpawnCooldown = TimeSpan.FromSeconds(60);

    [DataField, AutoNetworkedField]
    public TimeSpan? NextSpawnAt;
}
