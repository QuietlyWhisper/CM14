﻿using System.Linq;
using Content.Server.Administration;
using Content.Shared._RMC14.Marines;
using Content.Shared._RMC14.Marines.Skills;
using Content.Shared.Administration;
using Robust.Shared.Toolshed;
using Robust.Shared.Toolshed.Syntax;

namespace Content.Server._RMC14.SkillsCommand;

[ToolshedCommand, AdminCommand(AdminFlags.VarEdit)]
public sealed class SkillsCommand : ToolshedCommand
{
    private SkillsSystem? _skills;

    [CommandImplementation("get")]
    public int Get([PipedArgument] EntityUid marine, [CommandArgument] SkillType skill)
    {
        _skills ??= GetSys<SkillsSystem>();
        return _skills.GetSkill(marine, skill.Value);
    }

    [CommandImplementation("set")]
    public EntityUid Set(
        [CommandInvocationContext] IInvocationContext ctx,
        [PipedArgument] EntityUid marine,
        [CommandArgument] SkillType skill,
        [CommandArgument] ValueRef<int> level)
    {
        if (!HasComp<MarineComponent>(marine))
            return marine;

        _skills ??= GetSys<SkillsSystem>();

        var levelValue = level.Evaluate(ctx);
        _skills.SetSkill(marine, skill.Value, levelValue);

        return marine;
    }

    [CommandImplementation("set")]
    public IEnumerable<EntityUid> Set(
        [CommandInvocationContext] IInvocationContext ctx,
        [PipedArgument] IEnumerable<EntityUid> marines,
        [CommandArgument] SkillType skill,
        [CommandArgument] ValueRef<int> level)
    {
        return marines.Select(marine => Set(ctx, marine, skill, level));
    }
}
