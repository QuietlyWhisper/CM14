﻿using Content.Shared._RMC14.Inventory;
using Content.Shared.Containers.ItemSlots;
using Robust.Client.GameObjects;

namespace Content.Client._RMC14.Inventory;

public sealed class CMInventorySystem : SharedCMInventorySystem
{
    protected override void ContentsUpdated(Entity<CMItemSlotsComponent> ent)
    {
        base.ContentsUpdated(ent);

        if (!TryComp(ent, out SpriteComponent? sprite) ||
            !sprite.LayerMapTryGet(CMItemSlotsLayers.Fill, out var layer))
        {
            return;
        }

        if (!TryComp(ent, out ItemSlotsComponent? itemSlots))
        {
            sprite.LayerSetVisible(layer, false);
            return;
        }

        foreach (var (_, slot) in itemSlots.Slots)
        {
            if (slot.ContainerSlot?.ContainedEntity is { } contained &&
                !TerminatingOrDeleted(contained))
            {
                sprite.LayerSetVisible(layer, true);
                return;
            }
        }

        sprite.LayerSetVisible(layer, false);
    }

    protected override void ContentsUpdated(Entity<CMHolsterComponent> ent)
    {
        base.ContentsUpdated(ent);

        if (!TryComp(ent, out SpriteComponent? sprite) ||
            !sprite.LayerMapTryGet(CMHolsterLayers.Fill, out var layer))
        {
            return;
        }

        if (ent.Comp.Contents.Count != 0)
        {
            // TODO: implement per-gun underlay here
            // sprite.LayerSetState(layer, $"{<gun_state_here>}");
            sprite.LayerSetVisible(layer, true);

            // TODO: account for the gunslinger belt
            return;
        }

        sprite.LayerSetVisible(layer, false);
    }
}
