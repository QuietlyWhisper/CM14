using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._RMC14.BulletBox;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
[Access(typeof(BulletBoxSystem))]
public sealed partial class BulletBoxComponent : Component
{
    [DataField, AutoNetworkedField]
    public int Amount = 600;

    [DataField, AutoNetworkedField]
    public int Max = 600;

    [DataField, AutoNetworkedField]
    public bool TransferToBox = false;

    [DataField(required: true), AutoNetworkedField]
    public EntProtoId BulletType;

    [DataField, AutoNetworkedField]
    public TimeSpan DelayTransferFromBox = TimeSpan.FromSeconds(0.5);

    [DataField, AutoNetworkedField]
    public TimeSpan DelayTransferToBox = TimeSpan.FromSeconds(5);
}
