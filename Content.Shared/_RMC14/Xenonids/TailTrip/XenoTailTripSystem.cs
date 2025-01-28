using Content.Shared._RMC14.Slow;
using Content.Shared._RMC14.Xenonids.Finesse;
using Content.Shared._RMC14.Xenonids.Sweep;
using Content.Shared.Coordinates;
using Content.Shared.Movement.Systems;
using Content.Shared.Speech.EntitySystems;
using Content.Shared.Stunnable;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Network;

namespace Content.Shared._RMC14.Xenonids.TailTrip;

public sealed class XenoTailTripSystem : EntitySystem
{
    [Dependency] private readonly INetManager _net = default!;
    [Dependency] private readonly SharedStunSystem _stun = default!;
    [Dependency] private readonly SharedStutteringSystem _stutter = default!;
    [Dependency] private readonly MovementSpeedModifierSystem _speed = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly RMCSlowSystem _slow = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<XenoTailTripComponent, XenoTailTripActionEvent>(OnXenoTailTripAction);
    }

    private void OnXenoTailTripAction(Entity<XenoTailTripComponent> xeno, ref XenoTailTripActionEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;

        if (_net.IsServer)
            SpawnAttachedTo(xeno.Comp.TailEffect, args.Target.ToCoordinates());

        EnsureComp<XenoSweepingComponent>(xeno);
        _audio.PlayPredicted(xeno.Comp.Sound, xeno, xeno);

        if (HasComp<XenoMarkedComponent>(args.Target))
        {
            _stun.TryParalyze(args.Target, xeno.Comp.MarkedStunTime, true);
            // TODO RMC14 welder vision
            _stutter.DoStutter(args.Target, xeno.Comp.MarkedDazeTime, true);
            RemCompDeferred<XenoMarkedComponent>(args.Target);
        }
        else
        {
            _stun.TryParalyze(args.Target, xeno.Comp.StunTime, true);
            _slow.TrySlowdown(args.Target, xeno.Comp.SlowTime, ignoreDurationModifier: true);
        }
    }
}
