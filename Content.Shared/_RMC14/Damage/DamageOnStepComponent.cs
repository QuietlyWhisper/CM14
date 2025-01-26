using Content.Shared.Damage;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Content.Shared._RMC14.Damage;

/// <summary>
/// Whenever a valid entity voluntarly moves while on this entity, it takes damage.
/// If multiple valid entities are moving on this entity, all take damage on the same cooldown.
/// </summary>
[RegisterComponent]
public sealed partial class DamageOnStepComponent : Component
{
    /// <summary>
    /// Amount of damage to take, if t
    /// </summary>
    [DataField]
    public DamageSpecifier Damage = new();

    [DataField]
    public TimeSpan Cooldown = new(0);

    public TimeSpan NextDamageAt = new(0);
}
