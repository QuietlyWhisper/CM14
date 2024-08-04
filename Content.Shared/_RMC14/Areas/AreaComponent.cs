﻿using Robust.Shared.GameStates;

namespace Content.Shared._RMC14.Areas;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
[Access(typeof(AreaSystem))]
public sealed partial class AreaComponent : Component
{
    [DataField("CAS"), AutoNetworkedField]
    public bool CAS;

    [DataField, AutoNetworkedField]
    public bool Fulton;

    [DataField, AutoNetworkedField]
    public bool Lasing;

    [DataField, AutoNetworkedField]
    public bool Mortar;

    [DataField, AutoNetworkedField]
    public bool Medevac;

    [DataField("OB"), AutoNetworkedField]
    public bool OB;

    [DataField, AutoNetworkedField]
    public bool SupplyDrop;

    [DataField, AutoNetworkedField]
    public bool AvoidBioscan;

    [DataField, AutoNetworkedField]
    public bool NoTunnel;

    [DataField, AutoNetworkedField]
    public bool Unweedable;

    [DataField, AutoNetworkedField]
    public bool BuildSpecial;

    [DataField, AutoNetworkedField]
    public bool ResinAllowed = true;

    [DataField, AutoNetworkedField]
    public bool ResinConstructionAllowed = true;

    [DataField, AutoNetworkedField]
    public bool WeatherEnabled = true;

    [DataField, AutoNetworkedField]
    public bool HijackEvacuationArea;

    // TODO RMC14 does this need to be a double?
    [DataField, AutoNetworkedField]
    public double HijackEvacuationWeight;

    [DataField, AutoNetworkedField]
    public AreaHijackEvacuationType? HijackEvacuationType;

    [DataField, AutoNetworkedField]
    public string? PowerNet;

    [DataField, AutoNetworkedField]
    public Color MinimapColor;

    [DataField, AutoNetworkedField]
    public int ZLevel;
}
