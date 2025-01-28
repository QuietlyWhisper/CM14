using System.Numerics;
using Content.Shared._RMC14.Marines;
using Content.Shared._RMC14.Xenonids.Construction.Nest;
using Content.Shared._RMC14.Xenonids.Parasite;
using Content.Shared.ActionBlocker;
using Content.Shared.Coordinates;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.Drunk;
using Content.Shared.Jittering;
using Content.Shared.Mobs.Systems;
using Content.Shared.Popups;
using Content.Shared.Rejuvenate;
using Content.Shared.Speech.EntitySystems;
using Content.Shared.StatusEffect;
using Content.Shared.Throwing;
using Robust.Shared.Network;
using Robust.Shared.Player;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Random;
using Robust.Shared.Timing;
using Content.Shared.Projectiles;
using Content.Shared._RMC14.Pulling;
using Content.Shared._RMC14.Slow;

namespace Content.Shared._RMC14.Xenonids.Neurotoxin;

public abstract class SharedNeurotoxinSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly INetManager _net = default!;
    [Dependency] private readonly EntityLookupSystem _entityLookup = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly StaminaSystem _stamina = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly StatusEffectsSystem _statusEffects = default!;
    [Dependency] private readonly SharedSlurredSystem _slurred = default!;
    [Dependency] private readonly SharedStutteringSystem _stutter = default!;
    [Dependency] private readonly SharedJitteringSystem _jitter = default!;
    [Dependency] private readonly DamageableSystem _damage = default!;
    [Dependency] private readonly ThrowingSystem _throwing = default!; //It's how this fakes movement
    [Dependency] private readonly ActionBlockerSystem _blocker = default!;
    [Dependency] private readonly SharedPhysicsSystem _physics = default!;
    [Dependency] private readonly RMCPullingSystem _rmcPulling = default!;
    [Dependency] private readonly RMCSlowSystem _slow = default!;

    private readonly HashSet<Entity<MarineComponent>> _marines = new();
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<NeurotoxinComponent, RejuvenateEvent>(OnRejuvenate);
        SubscribeLocalEvent<NeurotoxinInjectorComponent, ProjectileHitEvent>(OnProjectileHit);
    }

    private void OnRejuvenate(Entity<NeurotoxinComponent> ent, ref RejuvenateEvent args)
    {
        RemCompDeferred<NeurotoxinComponent>(ent);
    }

    private void OnProjectileHit(Entity<NeurotoxinInjectorComponent> ent, ref ProjectileHitEvent args)
    {
        if (!HasComp<MarineComponent>(args.Target))
            return;

        if (!ent.Comp.AffectsDead && _mobState.IsDead(args.Target))
            return;

        if (!ent.Comp.AffectsInfectedNested &&
                    HasComp<XenoNestedComponent>(args.Target) &&
                    HasComp<VictimInfectedComponent>(args.Target))
        {
            return;
        }

        var time = _timing.CurTime;

        if (!EnsureComp<NeurotoxinComponent>(args.Target, out var neuro))
        {
            neuro.LastMessage = time;
            neuro.LastAccentTime = time;
            neuro.LastStumbleTime = time;
        }

        neuro.NeurotoxinAmount += ent.Comp.NeuroPerSecond;
        neuro.ToxinDamage = ent.Comp.ToxinDamage;
        neuro.OxygenDamage = ent.Comp.OxygenDamage;
        neuro.CoughDamage = ent.Comp.CoughDamage;
    }


    public override void Update(float frameTime)
    {
        if (_net.IsClient)
            return;

        var time = _timing.CurTime;
        var neurotoxinInjectorQuery = EntityQueryEnumerator<NeurotoxinInjectorComponent>();

        while (neurotoxinInjectorQuery.MoveNext(out var uid, out var neuroGas))
        {
            if (!neuroGas.InjectInContact)
                continue;

            _marines.Clear();
            _entityLookup.GetEntitiesInRange(uid.ToCoordinates(), 0.5f, _marines);

            foreach (var marine in _marines)
            {
                if (!neuroGas.AffectsDead && _mobState.IsDead(marine))
                    continue;

                if (!neuroGas.AffectsInfectedNested &&
                    HasComp<XenoNestedComponent>(marine) &&
                    HasComp<VictimInfectedComponent>(marine))
                {
                    continue;
                }

                if (!EnsureComp<NeurotoxinComponent>(marine, out var builtNeurotoxin))
                {
                    builtNeurotoxin.LastMessage = time;
                    builtNeurotoxin.LastAccentTime = time;
                    builtNeurotoxin.LastStumbleTime = time;
                    builtNeurotoxin.NextGasInjectionAt = time;
                }

                if (time < builtNeurotoxin.NextGasInjectionAt)
                    continue;

                // TODO RMC14 blurriness added here too
                builtNeurotoxin.NeurotoxinAmount += neuroGas.NeuroPerSecond;
                builtNeurotoxin.ToxinDamage = neuroGas.ToxinDamage;
                builtNeurotoxin.OxygenDamage = neuroGas.OxygenDamage;
                builtNeurotoxin.CoughDamage = neuroGas.CoughDamage;
                builtNeurotoxin.NextGasInjectionAt = time + neuroGas.TimeBetweenGasInjects;
            }
        }

        var neuroToxinQuery = EntityQueryEnumerator<NeurotoxinComponent>();

        while (neuroToxinQuery.MoveNext(out var uid, out var neuro))
        {
            neuro.NeurotoxinAmount -= frameTime * neuro.DepletionPerSecond;

            if(neuro.NeurotoxinAmount <= 0)
            {
                RemCompDeferred<NeurotoxinComponent>(uid);
                continue;
            }

            if (_mobState.IsDead(uid))
                continue;

            //Basic Effects
            _stamina.TakeStaminaDamage(uid, neuro.StaminaDamagePerSecond * frameTime, visual: false);
            _statusEffects.TryAddStatusEffect<DrunkComponent>(uid, "Drunk", neuro.DizzyStrength, true);

            NeurotoxinNonStackingEffects(uid, neuro, time, out var coughChance, out var stumbleChance);
            NeurotoxinStackingEffects(uid, neuro, frameTime, time);

            if (_random.Prob(stumbleChance * frameTime) && time - neuro.LastStumbleTime >= neuro.MinimumDelayBetweenEvents)
            {
                neuro.LastStumbleTime = time;
                // This is how we randomly move them - by throwing
                if (_blocker.CanMove(uid))
                {
                    _rmcPulling.TryStopPullsOn(uid);
                    _physics.SetLinearVelocity(uid, Vector2.Zero);
                    _physics.SetAngularVelocity(uid, 0f);
                    _throwing.TryThrow(uid, _random.NextAngle().ToVec().Normalized(), 1, animated: false, playSound: false, doSpin: false);
                }
                _popup.PopupEntity(Loc.GetString("rmc-stumble-others", ("victim", uid)), uid, Filter.PvsExcept(uid), true, PopupType.SmallCaution);
                _popup.PopupEntity(Loc.GetString("rmc-stumble"), uid, uid, PopupType.MediumCaution);
                _stutter.DoStutter(uid, neuro.DazeLength * 5, true);
                _jitter.DoJitter(uid, neuro.StumbleJitterTime, true);
                _statusEffects.TryAddStatusEffect<DrunkComponent>(uid, "Drunk", neuro.DizzyStrengthOnStumble, true);
                var ev = new NeurotoxinEmoteEvent() { Emote = neuro.PainId };
                RaiseLocalEvent(uid, ev);
            }

            if (_random.Prob(coughChance * frameTime))
            {
                _slow.TrySlowdown(uid, neuro.BloodCoughDuration);
                _damage.TryChangeDamage(uid, neuro.CoughDamage); // TODO RMC-14 specifically chest damage
                _popup.PopupEntity(Loc.GetString("rmc-bloodcough"), uid, uid, PopupType.MediumCaution);
                var ev = new NeurotoxinEmoteEvent() { Emote = neuro.CoughId };
                RaiseLocalEvent(uid, ev);
            }

        }

    }

    private void NeurotoxinNonStackingEffects(EntityUid victim, NeurotoxinComponent neurotoxin, TimeSpan time, out float coughChance, out float stumbleChance)
    {
        string message = "rmc-neuro-tired";
        PopupType poptype = PopupType.Small;
        coughChance = 0;
        stumbleChance = 0;
        if (neurotoxin.NeurotoxinAmount <= 9)
        {
            //Do nothing, the intial conditions are already set
        }
        else if (neurotoxin.NeurotoxinAmount <= 14)
        {
            message = "rmc-neuro-numb";
            poptype = PopupType.SmallCaution;
            coughChance = 0.10f;
        }
        else if (neurotoxin.NeurotoxinAmount <= 19)
        {
            int chance = _random.Next(4);
            if(chance == 0)
            {
                message = "rmc-neuro-where";
                poptype = PopupType.Large;
            }
            else
            {
                message = _random.Pick(new List<string> {"rmc-neuro-very-numb", "rmc-neuro-erratic", "rmc-neuro-panic"});
                poptype = PopupType.MediumCaution;
            }
            coughChance = 0.10f;
            stumbleChance = 0.05f;
        }
        else if (neurotoxin.NeurotoxinAmount <= 24)
        {
            message = "rmc-neuro-sting";
            poptype = PopupType.MediumCaution;
            coughChance = 0.25f;
            stumbleChance = 0.25f;

        }
        else
        {
            int chance = _random.Next(7);
            if (chance == 0)
            {
                message = "rmc-neuro-what";
                poptype = PopupType.Large;
            }
            else if (chance == 1)
            {
                message = "rmc-neuro-hearing";
                poptype = PopupType.MediumCaution;
            }
            else
            {
                message = _random.Pick(new List<string> { "rmc-neuro-pain", "rmc-neuro-agh", "rmc-neuro-so-numb", "rmc-neuro-limbs", "rmc-neuro-think"});
                poptype = PopupType.LargeCaution;
            }
            coughChance = 0.25f;
            stumbleChance = 0.25f;
        }

        if (time - neurotoxin.LastMessage >= neurotoxin.TimeBetweenMessages)
        {
            neurotoxin.LastMessage = time;
            _popup.PopupEntity(Loc.GetString(message), victim, victim, poptype);
        }
    }

    private void NeurotoxinStackingEffects(EntityUid victim, NeurotoxinComponent neurotoxin, float frameTime, TimeSpan currTime)
    {
        if (neurotoxin.NeurotoxinAmount >= 10)
        {
            // TODO RMC14 eye blur here
            if (currTime - neurotoxin.LastAccentTime >= neurotoxin.MinimumDelayBetweenEvents)
            {
                neurotoxin.LastAccentTime = currTime;
                if (_random.Prob(0.5f))
                    _slurred.DoSlur(victim, neurotoxin.AccentTime);
                else
                    _stutter.DoStutter(victim, neurotoxin.AccentTime, true);
            }
        }

        if (neurotoxin.NeurotoxinAmount >= 15)
        {
            // TODO RMC14 Agony effect - gives fake damage, pain needs this too so maybe then
            _jitter.DoJitter(victim, neurotoxin.JitterTime, true);
            // TODO RMC14 Hallucinations would and be checked and then done through a function
            // Will need...alot of work
        }

        if (neurotoxin.NeurotoxinAmount >= 20)
        {
            // _statusEffects.TryAddStatusEffect(victim, "TemporaryBlindness", neurotoxin.BlindTime, true, "TemporaryBlindness");
        }

        if (neurotoxin.NeurotoxinAmount >= 27)
        {
            // TODO RMC14 gives weldervision too
            _stutter.DoStutter(victim, neurotoxin.DazeLength, true);
            _damage.TryChangeDamage(victim, neurotoxin.ToxinDamage * frameTime);
            // TODO RMC14 tempoarary deafness
        }

        if (neurotoxin.NeurotoxinAmount >= 50)
        {
            // TODO RMC14 also gives liver damage
            _damage.TryChangeDamage(victim, neurotoxin.OxygenDamage * frameTime);
        }
    }
}
