using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared._RMC14.Xenonids.Fruit.Components;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
[Access(typeof(XenoFruitVisualsSystem))]
public sealed partial class XenoFruitVisualsComponent : Component
{
    [DataField(required: true), AutoNetworkedField]
    public string Rsi;

    [DataField(required: true), AutoNetworkedField]
    public string Prefix;

    [DataField, AutoNetworkedField]
    public Color? Color;
}

[Serializable, NetSerializable]
public enum XenoFruitVisuals
{
    Resting,
    Downed,
    Color,
}

[Serializable, NetSerializable]
public enum XenoFruitVisualLayers
{
    Base,
}
