using System.Numerics;
using Content.Client._RMC14.Medical.HUD;
using Content.Client._RMC14.NightVision;
using Content.Shared._RMC14.Mobs;
using Content.Shared._RMC14.Shields;
using Content.Shared._RMC14.Stealth;
using Content.Shared._RMC14.Xenonids;
using Content.Shared._RMC14.Xenonids.Energy;
using Content.Shared._RMC14.Xenonids.Maturing;
using Content.Shared._RMC14.Xenonids.Parasite;
using Content.Shared._RMC14.Xenonids.Plasma;
using Content.Shared._RMC14.Xenonids.Projectile.Spit.Stacks;
using Content.Shared._RMC14.Xenonids.Rank;
using Content.Shared.Damage;
using Content.Shared.Ghost;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Rounding;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Client.ResourceManagement;
using Robust.Shared.Enums;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;
using Robust.Shared.Utility;
using Content.Shared._RMC14.Xenonids.Finesse;
using static Robust.Shared.Utility.SpriteSpecifier;
using Content.Shared._RMC14.Slow;

namespace Content.Client._RMC14.Xenonids.Hud;

public sealed class XenoHudOverlay : Overlay
{
    [Dependency] private readonly IEntityManager _entity = default!;
    [Dependency] private readonly IOverlayManager _overlay = default!;
    [Dependency] private readonly IPlayerManager _players = default!;
    [Dependency] private readonly IPrototypeManager _prototype = default!;
    [Dependency] private readonly IResourceCache _resourceCache = default!;
    [Dependency] private readonly IGameTiming _timing = default!;

    private readonly ContainerSystem _container;
    private readonly CMHealthIconsSystem _healthIcons;
    private readonly MobStateSystem _mobState;
    private readonly MobThresholdSystem _mobThresholds;
    private readonly SpriteSystem _sprite;
    private readonly TransformSystem _transform;

    private readonly EntityQuery<DamageableComponent> _damageableQuery;
    private readonly EntityQuery<XenoParasiteComponent> _xenoParasiteQuery;
    private readonly EntityQuery<MobStateComponent> _mobStateQuery;
    private readonly EntityQuery<MobThresholdsComponent> _mobThresholdsQuery;
    private readonly EntityQuery<XenoEnergyComponent> _xenoEnergyQuery;
    private readonly EntityQuery<XenoMaturingComponent> _xenoMaturingQuery;
    private readonly EntityQuery<XenoPlasmaComponent> _xenoPlasmaQuery;
    private readonly EntityQuery<TransformComponent> _xformQuery;
    private readonly EntityQuery<XenoShieldComponent> _xenoShieldQuery;
    private readonly EntityQuery<EntityActiveInvisibleComponent> _invisQuery;
    private readonly EntityQuery<XenoComponent> _xenoQuery;

    private readonly ShaderInstance _shader;

    public override OverlaySpace Space => _overlay.HasOverlay<NightVisionOverlay>()
        ? OverlaySpace.WorldSpace
        : OverlaySpace.WorldSpaceBelowFOV;

    private readonly ResPath _rsiPath = new("/Textures/_RMC14/Interface/xeno_hud.rsi");
    private readonly ResPath _rsiPathSlow = new("/Textures/_RMC14/Effects/xeno_stomp.rsi");
    private readonly ResPath _rsiPathFreeze = new("/Textures/_RMC14/Effects/xeno_freeze.rsi");

    public XenoHudOverlay()
    {
        IoCManager.InjectDependencies(this);

        _container = _entity.System<ContainerSystem>();
        _healthIcons = _entity.System<CMHealthIconsSystem>();
        _mobState = _entity.System<MobStateSystem>();
        _mobThresholds = _entity.System<MobThresholdSystem>();
        _sprite = _entity.System<SpriteSystem>();
        _transform = _entity.System<TransformSystem>();

        _damageableQuery = _entity.GetEntityQuery<DamageableComponent>();
        _xenoParasiteQuery = _entity.GetEntityQuery<XenoParasiteComponent>();
        _mobStateQuery = _entity.GetEntityQuery<MobStateComponent>();
        _mobThresholdsQuery = _entity.GetEntityQuery<MobThresholdsComponent>();
        _xenoEnergyQuery = _entity.GetEntityQuery<XenoEnergyComponent>();
        _xenoMaturingQuery = _entity.GetEntityQuery<XenoMaturingComponent>();
        _xenoPlasmaQuery = _entity.GetEntityQuery<XenoPlasmaComponent>();
        _xformQuery = _entity.GetEntityQuery<TransformComponent>();
        _xenoShieldQuery = _entity.GetEntityQuery<XenoShieldComponent>();
        _invisQuery = _entity.GetEntityQuery<EntityActiveInvisibleComponent>();
        _xenoQuery = _entity.GetEntityQuery<XenoComponent>();

        _shader = _prototype.Index<ShaderPrototype>("unshaded").Instance();
        ZIndex = 1;
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        var isAdminGhost = _entity.TryGetComponent(_players.LocalEntity, out GhostComponent? ghost) &&
                           ghost.CanGhostInteract;
        var isXeno = _entity.HasComponent<XenoComponent>(_players.LocalEntity);
        var isGhost = false;

        if (!_entity.HasComponent<CMGhostXenoHudComponent>(_players.LocalEntity))
        {
            if (!isXeno && !isAdminGhost)
                return;
        }
        else
        {
            if (_entity.HasComponent<CMGhostXenoHudComponent>(_players.LocalEntity))
                isGhost = true;
            isXeno = true;
        }
        var handle = args.WorldHandle;
        var eyeRot = args.Viewport.Eye?.Rotation ?? default;

        var scaleMatrix = Matrix3x2.CreateScale(new Vector2(1, 1));
        var rotationMatrix = Matrix3Helpers.CreateRotation(-eyeRot);

        handle.UseShader(_shader);

        if (isXeno)
        {
            DrawBars(in args, scaleMatrix, rotationMatrix);
            if (!isGhost)
                DrawDeadIcon(in args, scaleMatrix, rotationMatrix);

            DrawAcidStacks(in args, scaleMatrix, rotationMatrix);
            DrawMarkedIcons(in args, scaleMatrix, rotationMatrix);
            DrawRank(in args, scaleMatrix, rotationMatrix);

            DrawSlow(in args, scaleMatrix, rotationMatrix);
            DrawStun(in args, scaleMatrix, rotationMatrix);
        }

        if (isXeno || isAdminGhost)
            DrawInfectedIcon(in args, scaleMatrix, rotationMatrix);

        handle.UseShader(null);
        handle.SetTransform(Matrix3x2.Identity);
    }

    private void DrawBars(in OverlayDrawArgs args, Matrix3x2 scaleMatrix, Matrix3x2 rotationMatrix)
    {
        var handle = args.WorldHandle;
        var xenos = _entity.AllEntityQueryEnumerator<XenoComponent, SpriteComponent, TransformComponent>();
        while (xenos.MoveNext(out var uid, out var xeno, out var sprite, out var xform))
        {
            if (xform.MapID != args.MapId)
                continue;

            if (_container.IsEntityOrParentInContainer(uid, xform: xform))
                continue;

            var bounds = sprite.Bounds;
            var worldPos = _transform.GetWorldPosition(xform, _xformQuery);

            if (!bounds.Translated(worldPos).Intersects(args.WorldAABB))
                continue;

            var worldMatrix = Matrix3x2.CreateTranslation(worldPos);
            var scaledWorld = Matrix3x2.Multiply(scaleMatrix, worldMatrix);
            var matrix = Matrix3x2.Multiply(rotationMatrix, scaledWorld);
            handle.SetTransform(matrix);

            if (_mobStateQuery.TryComp(uid, out var mobState) &&
                _mobState.IsDead(uid, mobState))
            {
                continue;
            }

            UpdateHealth((uid, xeno, sprite, mobState), handle);
            UpdatePlasma((uid, xeno, sprite), handle);
            UpdateShields((uid, xeno, sprite), handle);
            UpdateEnergy((uid, xeno, sprite), handle);
        }
    }

    private void DrawDeadIcon(in OverlayDrawArgs args, Matrix3x2 scaleMatrix, Matrix3x2 rotationMatrix)
    {
        var icon = _healthIcons.GetDeadIcon().Icon;
        var handle = args.WorldHandle;
        var infected = _entity.AllEntityQueryEnumerator<MobStateComponent, SpriteComponent, TransformComponent>();
        while (infected.MoveNext(out var uid, out var comp, out var sprite, out var xform))
        {
            if (xform.MapID != args.MapId)
                continue;

            if (comp.CurrentState != MobState.Dead)
                continue;

            if (_container.IsEntityOrParentInContainer(uid, xform: xform))
                continue;

            if (_xenoParasiteQuery.HasComp(uid))
                continue;

            if (_invisQuery.HasComp(uid))
                continue;

            var bounds = sprite.Bounds;
            var worldPos = _transform.GetWorldPosition(xform, _xformQuery);

            if (!bounds.Translated(worldPos).Intersects(args.WorldAABB))
                continue;

            var worldMatrix = Matrix3x2.CreateTranslation(worldPos);
            var scaledWorld = Matrix3x2.Multiply(scaleMatrix, worldMatrix);
            var matrix = Matrix3x2.Multiply(rotationMatrix, scaledWorld);
            handle.SetTransform(matrix);

            var texture = _sprite.GetFrame(icon, _timing.CurTime);

            var yOffset = (bounds.Height + sprite.Offset.Y) / 2f - (float) texture.Height / EyeManager.PixelsPerMeter * bounds.Height;
            var xOffset = (bounds.Width + sprite.Offset.X) / 2f - (float) texture.Width / EyeManager.PixelsPerMeter * bounds.Width;

            var position = new Vector2(xOffset, yOffset);
            handle.DrawTexture(texture, position);
        }
    }

    private void DrawAcidStacks(in OverlayDrawArgs args, Matrix3x2 scaleMatrix, Matrix3x2 rotationMatrix)
    {
        var handle = args.WorldHandle;
        var stacks = _entity
            .AllEntityQueryEnumerator<VictimXenoAcidStacksComponent, SpriteComponent, TransformComponent>();
        while (stacks.MoveNext(out var uid, out var comp, out var sprite, out var xform))
        {
            if (xform.MapID != args.MapId)
                continue;

            if (_container.IsEntityOrParentInContainer(uid, xform: xform))
                continue;

            if (_invisQuery.HasComp(uid))
                continue;

            var bounds = sprite.Bounds;
            var worldPos = _transform.GetWorldPosition(xform, _xformQuery);

            if (!bounds.Translated(worldPos).Intersects(args.WorldAABB))
                continue;

            var worldMatrix = Matrix3x2.CreateTranslation(worldPos);
            var scaledWorld = Matrix3x2.Multiply(scaleMatrix, worldMatrix);
            var matrix = Matrix3x2.Multiply(rotationMatrix, scaledWorld);
            handle.SetTransform(matrix);

            var level = Math.Clamp(comp.Current, 0, 4);
            var icon = new Rsi(_rsiPath, $"acid_stacks{level}");
            var texture = _sprite.GetFrame(icon, _timing.CurTime);

            var yOffset = (bounds.Height + sprite.Offset.Y) / 2f - (float) texture.Height / EyeManager.PixelsPerMeter * bounds.Height;
            var xOffset = (bounds.Width + sprite.Offset.X) / 2f - (float) texture.Width / EyeManager.PixelsPerMeter * bounds.Width;

            var position = new Vector2(xOffset, yOffset);
            handle.DrawTexture(texture, position);
        }
    }

    private void DrawRank(in OverlayDrawArgs args, Matrix3x2 scaleMatrix, Matrix3x2 rotationMatrix)
    {
        var handle = args.WorldHandle;
        var ranks = _entity.EntityQueryEnumerator<XenoRankComponent, SpriteComponent, TransformComponent>();
        while (ranks.MoveNext(out var uid, out var comp, out var sprite, out var xform))
        {
            if (comp.Rank < 2 || comp.Rank > 5 || _xenoMaturingQuery.HasComp(uid))
                continue;

            if (xform.MapID != args.MapId)
                continue;

            if (_container.IsEntityOrParentInContainer(uid, xform: xform))
                continue;

            if (_invisQuery.HasComp(uid))
                continue;

            var bounds = sprite.Bounds;
            var worldPos = _transform.GetWorldPosition(xform, _xformQuery);

            if (!bounds.Translated(worldPos).Intersects(args.WorldAABB))
                continue;

            var worldMatrix = Matrix3x2.CreateTranslation(worldPos);
            var scaledWorld = Matrix3x2.Multiply(scaleMatrix, worldMatrix);
            var matrix = Matrix3x2.Multiply(rotationMatrix, scaledWorld);
            handle.SetTransform(matrix);

            var icon = new Rsi(_rsiPath, $"hudxenoupgrade{comp.Rank}");
            var texture = _sprite.GetFrame(icon, _timing.CurTime);

            var yOffset = (bounds.Height + sprite.Offset.Y) / 2f - (float) texture.Height / EyeManager.PixelsPerMeter * bounds.Height;
            var xOffset = (bounds.Width + sprite.Offset.X) / 2f - (float) texture.Width / EyeManager.PixelsPerMeter * bounds.Width;

            var position = new Vector2(xOffset, yOffset);
            handle.DrawTexture(texture, position);
        }
    }

    private void DrawMarkedIcons(in OverlayDrawArgs args, Matrix3x2 scaleMatrix, Matrix3x2 rotationMatrix)
    {
        var handle = args.WorldHandle;
        var stacks = _entity
            .AllEntityQueryEnumerator<XenoMarkedComponent, SpriteComponent, TransformComponent>();

        while (stacks.MoveNext(out var uid, out var comp, out var sprite, out var xform))
        {
            if (xform.MapID != args.MapId)
                continue;

            if (_container.IsEntityOrParentInContainer(uid, xform: xform))
                continue;

            if (_invisQuery.HasComp(uid))
                continue;

            var bounds = sprite.Bounds;
            var worldPos = _transform.GetWorldPosition(xform, _xformQuery);

            if (!bounds.Translated(worldPos).Intersects(args.WorldAABB))
                continue;

            var worldMatrix = Matrix3x2.CreateTranslation(worldPos);
            var scaledWorld = Matrix3x2.Multiply(scaleMatrix, worldMatrix);
            var matrix = Matrix3x2.Multiply(rotationMatrix, scaledWorld);
            handle.SetTransform(matrix);

            var icon = new Rsi(_rsiPath, $"prae_tag");
            var texture = _sprite.GetFrame(icon, _timing.CurTime - comp.TimeAdded, false);

            var yOffset = (bounds.Height + sprite.Offset.Y) / 2f - (float)texture.Height / EyeManager.PixelsPerMeter * bounds.Height;
            var xOffset = (bounds.Width + sprite.Offset.X) / 2f - (float)texture.Width / EyeManager.PixelsPerMeter * bounds.Width;

            var position = new Vector2(xOffset, yOffset);
            handle.DrawTexture(texture, position);
        }
    }

    private void DrawSlow(in OverlayDrawArgs args, Matrix3x2 scaleMatrix, Matrix3x2 rotationMatrix)
    {
        var handle = args.WorldHandle;
        var slows = _entity
            .AllEntityQueryEnumerator<XenoSlowVisualsComponent, SpriteComponent, TransformComponent>();

        while (slows.MoveNext(out var uid, out var comp, out var sprite, out var xform))
        {
            if (xform.MapID != args.MapId)
                continue;

            if (_container.IsEntityOrParentInContainer(uid, xform: xform))
                continue;

            if (_invisQuery.HasComp(uid))
                continue;

            if (_xenoQuery.HasComp(uid))
                continue;

            var bounds = sprite.Bounds;
            var worldPos = _transform.GetWorldPosition(xform, _xformQuery);

            if (!bounds.Translated(worldPos).Intersects(args.WorldAABB))
                continue;

            var worldMatrix = Matrix3x2.CreateTranslation(worldPos);
            var scaledWorld = Matrix3x2.Multiply(scaleMatrix, worldMatrix);
            var matrix = Matrix3x2.Multiply(rotationMatrix, scaledWorld);
            handle.SetTransform(matrix);

            var icon = new Rsi(_rsiPathSlow, $"stomp");
            var texture = _sprite.GetFrame(icon, _timing.CurTime);

            var yOffset = (bounds.Height + sprite.Offset.Y) / 2f - (float)texture.Height / EyeManager.PixelsPerMeter * bounds.Height;
            var xOffset = (bounds.Width + sprite.Offset.X) / 2f - (float)texture.Width / EyeManager.PixelsPerMeter * bounds.Width;

            var position = new Vector2(xOffset, yOffset);
            handle.DrawTexture(texture, position);
        }
    }

    private void DrawStun(in OverlayDrawArgs args, Matrix3x2 scaleMatrix, Matrix3x2 rotationMatrix)
    {
        var handle = args.WorldHandle;
        var slows = _entity
            .AllEntityQueryEnumerator<XenoImmobileVisualsComponent, SpriteComponent, TransformComponent>();

        while (slows.MoveNext(out var uid, out var comp, out var sprite, out var xform))
        {
            if (xform.MapID != args.MapId)
                continue;

            if (_container.IsEntityOrParentInContainer(uid, xform: xform))
                continue;

            if (_invisQuery.HasComp(uid))
                continue;

            if (_xenoQuery.HasComp(uid))
                continue;

            var bounds = sprite.Bounds;
            var worldPos = _transform.GetWorldPosition(xform, _xformQuery);

            if (!bounds.Translated(worldPos).Intersects(args.WorldAABB))
                continue;

            var worldMatrix = Matrix3x2.CreateTranslation(worldPos);
            var scaledWorld = Matrix3x2.Multiply(scaleMatrix, worldMatrix);
            var matrix = Matrix3x2.Multiply(rotationMatrix, scaledWorld);
            handle.SetTransform(matrix);

            var icon = new Rsi(_rsiPathFreeze, $"freeze");
            var texture = _sprite.GetFrame(icon, _timing.CurTime);

            var yOffset = (bounds.Height + sprite.Offset.Y) / 2f - (float)texture.Height / EyeManager.PixelsPerMeter * bounds.Height;
            var xOffset = (bounds.Width + sprite.Offset.X) / 2f - (float)texture.Width / EyeManager.PixelsPerMeter * bounds.Width;

            var position = new Vector2(xOffset, yOffset);
            handle.DrawTexture(texture, position);
        }
    }

    private void DrawInfectedIcon(in OverlayDrawArgs args, Matrix3x2 scaleMatrix, Matrix3x2 rotationMatrix)
    {
        var handle = args.WorldHandle;
        var infected = _entity.AllEntityQueryEnumerator<VictimInfectedComponent, SpriteComponent, TransformComponent>();
        while (infected.MoveNext(out var uid, out var comp, out var sprite, out var xform))
        {
            if (xform.MapID != args.MapId)
                continue;

            if (_container.IsEntityOrParentInContainer(uid, xform: xform))
                continue;

            if (_invisQuery.HasComp(uid))
                continue;

            var bounds = sprite.Bounds;
            var worldPos = _transform.GetWorldPosition(xform, _xformQuery);

            if (!bounds.Translated(worldPos).Intersects(args.WorldAABB))
                continue;

            var worldMatrix = Matrix3x2.CreateTranslation(worldPos);
            var scaledWorld = Matrix3x2.Multiply(scaleMatrix, worldMatrix);
            var matrix = Matrix3x2.Multiply(rotationMatrix, scaledWorld);
            handle.SetTransform(matrix);

            var level = Math.Min(comp.CurrentStage, comp.InfectedIcons.Length - 1);
            var icon = comp.InfectedIcons[level];
            var texture = _sprite.GetFrame(icon, _timing.CurTime);

            var yOffset = (bounds.Height + sprite.Offset.Y) / 2f - (float) texture.Height / EyeManager.PixelsPerMeter * bounds.Height;
            var xOffset = (bounds.Width + sprite.Offset.X) / 2f - (float) texture.Width / EyeManager.PixelsPerMeter * bounds.Width;

            var position = new Vector2(xOffset, yOffset);
            handle.DrawTexture(texture, position);
        }
    }

    private void UpdateHealth(Entity<XenoComponent, SpriteComponent, MobStateComponent?> ent, DrawingHandleWorld handle)
    {
        var (uid, xeno, sprite, mobState) = ent;
        if (!_damageableQuery.TryComp(uid, out var damageable))
            return;

        var damage = damageable.TotalDamage;
        var mobThresholds = _mobThresholdsQuery.CompOrNull(uid);
        _mobThresholds.TryGetThresholdForState(uid, MobState.Critical, out var critThresholdNullable, mobThresholds);
        _mobThresholds.TryGetDeadThreshold(uid, out var deadThresholdNullable, mobThresholds);

        string state;
        if (_mobState.IsCritical(uid, mobState))
        {
            if (critThresholdNullable is not { } critThreshold || deadThresholdNullable is not { } deadThreshold)
                return;

            deadThreshold -= critThreshold;
            damage -= critThreshold;
            var level = ContentHelpers.RoundToLevels(damage.Double(), deadThreshold.Double(), 11);
            var name = level > 0 ? $"{level * 10}" : "1";
            state = $"xenohealth-{name}";
        }
        else
        {
            critThresholdNullable ??= deadThresholdNullable;
            if (critThresholdNullable == null)
                return;

            var level = ContentHelpers.RoundToLevels((critThresholdNullable - damage).Value.Double(), critThresholdNullable.Value.Double(), 11);
            var name = level > 0 ? $"{level * 10}" : "0";
            state = $"xenohealth{name}";
        }

        var icon = new Rsi(_rsiPath, state);
        var rsi = _resourceCache.GetResource<RSIResource>(icon.RsiPath).RSI;
        if (!rsi.TryGetState(icon.RsiState, out _))
            return;

        var texture = _sprite.GetFrame(icon, _timing.CurTime);

        var bounds = sprite.Bounds;
        var yOffset = (bounds.Height + sprite.Offset.Y) / 2f - (float) texture.Height / EyeManager.PixelsPerMeter * bounds.Height + xeno.HudOffset.Y;
        var xOffset = (bounds.Width + sprite.Offset.X) / 2f - (float) texture.Width / EyeManager.PixelsPerMeter * bounds.Width + xeno.HudOffset.X;

        var position = new Vector2(xOffset, yOffset);
        handle.DrawTexture(texture, position);
    }

    private void UpdatePlasma(Entity<XenoComponent, SpriteComponent> ent, DrawingHandleWorld handle)
    {
        var (uid, xeno, sprite) = ent;
        if (!_xenoPlasmaQuery.TryComp(uid, out var comp) ||
            comp.MaxPlasma == 0)
        {
            return;
        }

        var plasma = comp.Plasma;
        var max = comp.MaxPlasma;
        var level = ContentHelpers.RoundToLevels(plasma.Double(), max, 11);
        var name = level > 0 ? $"{level * 10}" : "0";
        var state = $"plasma{name}";
        var icon = new Rsi(new ResPath("/Textures/_RMC14/Interface/xeno_hud.rsi"), state);
        var texture = _sprite.GetFrame(icon, _timing.CurTime);

        var bounds = sprite.Bounds;
        var yOffset = (bounds.Height + sprite.Offset.Y) / 2f - (float) texture.Height / EyeManager.PixelsPerMeter * bounds.Height + xeno.HudOffset.Y;
        var xOffset = (bounds.Width + sprite.Offset.X) / 2f - (float) texture.Width / EyeManager.PixelsPerMeter * bounds.Width + xeno.HudOffset.X;

        var position = new Vector2(xOffset, yOffset);
        handle.DrawTexture(texture, position);
    }

    private void UpdateShields(Entity<XenoComponent, SpriteComponent> ent, DrawingHandleWorld handle)
    {
        var (uid, xeno, sprite) = ent;
        if (!_xenoShieldQuery.TryComp(uid, out var comp))
            return;

        var mobThresholds = _mobThresholdsQuery.CompOrNull(uid);
        _mobThresholds.TryGetThresholdForState(uid, MobState.Critical, out var critThresholdNullable, mobThresholds);
        _mobThresholds.TryGetDeadThreshold(uid, out var deadThresholdNullable, mobThresholds);

        critThresholdNullable ??= deadThresholdNullable;
        if (critThresholdNullable == null)
            return;

        var shield = comp.ShieldAmount;
        var max = critThresholdNullable.Value.Double();
        var level = ContentHelpers.RoundToLevels(shield.Double(), max, 11);
        var name = level > 0 ? $"{level * 10}" : "0";
        var state = $"xenoshield{name}";
        var icon = new Rsi(new ResPath("/Textures/_RMC14/Interface/xeno_hud.rsi"), state);
        var texture = _sprite.GetFrame(icon, _timing.CurTime);

        var bounds = sprite.Bounds;
        var yOffset = (bounds.Height + sprite.Offset.Y) / 2f - (float)texture.Height / EyeManager.PixelsPerMeter * bounds.Height + xeno.HudOffset.Y;
        var xOffset = (bounds.Width + sprite.Offset.X) / 2f - (float)texture.Width / EyeManager.PixelsPerMeter * bounds.Width + xeno.HudOffset.X;

        var position = new Vector2(xOffset, yOffset);
        handle.DrawTexture(texture, position);
    }

    private void UpdateEnergy(Entity<XenoComponent, SpriteComponent> ent, DrawingHandleWorld handle)
    {
        var (uid, xeno, sprite) = ent;
        if (!_xenoEnergyQuery.TryComp(uid, out var comp) ||
            comp.Max == 0)
        {
            return;
        }

        var energy = comp.Current;
        var max = comp.Max;
        var level = ContentHelpers.RoundToLevels(energy, max, 11);
        var name = level > 0 ? $"{level * 10}" : "0";
        var state = $"xenoenergy{name}";
        var icon = new Rsi(new ResPath("/Textures/_RMC14/Interface/xeno_hud.rsi"), state);
        var texture = _sprite.GetFrame(icon, _timing.CurTime);

        var bounds = sprite.Bounds;
        var yOffset = (bounds.Height + sprite.Offset.Y) / 2f - (float) texture.Height / EyeManager.PixelsPerMeter * bounds.Height + xeno.HudOffset.Y;
        var xOffset = (bounds.Width + sprite.Offset.X) / 2f - (float) texture.Width / EyeManager.PixelsPerMeter * bounds.Width + xeno.HudOffset.X;

        var position = new Vector2(xOffset, yOffset);
        handle.DrawTexture(texture, position);
    }
}
