﻿using Content.Shared.Access.Components;
using Content.Shared.Access.Systems;
using Content.Shared.Damage;
using Content.Shared.Examine;
using Content.Shared.HealthExaminable;
using Content.Shared.Verbs;

namespace Content.Shared._CM14.Examine;

public sealed class CMExamineSystem : EntitySystem
{
    [Dependency] private readonly HealthExaminableSystem _healthExaminable = default!;
    [Dependency] private readonly IdExaminableSystem _idExaminable = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<ShortExamineComponent, GetVerbsEvent<ExamineVerb>>(OnGetExaminedVerbs, after: [typeof(HealthExaminableSystem), typeof(IdExaminableSystem)]);
        SubscribeLocalEvent<IdExaminableComponent, ExaminedEvent>(OnIdExamined);
        SubscribeLocalEvent<HealthExaminableComponent, ExaminedEvent>(OnHealthExamined);
    }

    private void OnGetExaminedVerbs(Entity<ShortExamineComponent> ent, ref GetVerbsEvent<ExamineVerb> args)
    {
        args.Verbs.RemoveWhere(v =>
            v.Text == Loc.GetString("health-examinable-verb-text") ||
            v.Text == Loc.GetString("id-examinable-component-verb-text")
        );
    }

    private void OnIdExamined(Entity<IdExaminableComponent> ent, ref ExaminedEvent args)
    {
        using (args.PushGroup(nameof(CMExamineSystem), 1))
        {
            if (_idExaminable.GetInfo(ent) is { } info)
                args.PushMarkup(info);
        }
    }

    private void OnHealthExamined(Entity<HealthExaminableComponent> ent, ref ExaminedEvent args)
    {
        using (args.PushGroup(nameof(CMExamineSystem), -1))
        {
            if (TryComp(ent, out DamageableComponent? damageable))
            {
                args.PushMessage(_healthExaminable.CreateMarkup(ent, ent.Comp, damageable));
            }
        }
    }
}
