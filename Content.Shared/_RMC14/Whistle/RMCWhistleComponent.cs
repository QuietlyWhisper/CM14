using Robust.Shared.GameStates;
using Content.Shared.Humanoid;
using Content.Shared.Whistle;
using Robust.Shared.Prototypes;

namespace Content.Shared._RMC14.Whistle;

/// <summary>
/// Spawn attached entity for entities in range with <see cref="HumanoidAppearanceComponent"/>.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class RMCWhistleComponent : WhistleComponent
{
    [DataField, AutoNetworkedField]
    public EntProtoId ActionId = "RMCActionWhistle";

    [DataField, AutoNetworkedField]
    public EntityUid? Action;
}
