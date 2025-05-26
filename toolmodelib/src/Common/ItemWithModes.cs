using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;
using Vintagestory.API.Util;

namespace ToolModeLib;

public abstract class ItemWithModes : Item, ICollectibleWithModes
{
    /// <summary>
    /// Tool modes registered to this specific item.
    /// </summary>
    public ToolMode[] ToolModes = Array.Empty<ToolMode>();

    /// <summary>
    /// Tool mode group used to automatically instantiate relevant tool modes.
    /// </summary>
    public virtual AssetLocation Group => null;

    public override void OnLoaded(ICoreAPI api)
    {
        base.OnLoaded(api);
        if(Group != null)
            ToolModes = api.GetToolModeRegistry().CreateToolModeGroup(Group);

        JsonObject[] toolModesArray = Attributes["toolmodes"]?.AsArray();
        List<ToolMode> itemSpecificModes = new();
        if(toolModesArray?.Any() ?? false) {
            foreach(JsonObject modeObj in toolModesArray) {
                AssetLocation modeCode = modeObj["name"].AsObject<AssetLocation>();
                JsonObject modeProperties = modeObj["properties"];

                ToolMode mode = api.GetToolModeRegistry().CreateToolMode(this, modeCode);
                if(modeProperties != null) mode.Initialize(modeProperties);

                itemSpecificModes.Add(mode);
            }
        }

        ToolModes = ToolModes.Append(itemSpecificModes);
        foreach(ToolMode mode in ToolModes) {
            mode.OnLoaded(api);
        }
    }

    public override void OnUnloaded(ICoreAPI api)
    {
        base.OnUnloaded(api);
        foreach(ToolMode mode in ToolModes) {
            mode.OnUnloaded(api);
        }
    }

    public override WorldInteraction[] GetHeldInteractionHelp(ItemSlot inSlot)
    {
        WorldInteraction[] interactions = base.GetHeldInteractionHelp(inSlot);
        IClientPlayer player = (api as ICoreClientAPI).World.Player;
        ToolMode mode = GetRealToolMode(player);

        if(mode != null) {
            interactions = interactions.Append(mode.GetHeldInteractionHelp(inSlot));
        }

        return interactions.Append(
            new WorldInteraction()
            {
                ActionLangCode = "heldhelp-settoolmode",
                HotKeyCode = "toolmodeselect"
            });
    }

    public override SkillItem[] GetToolModes(ItemSlot slot, IClientPlayer forPlayer, BlockSelection blockSel)
    {
        List<SkillItem> skillItems = new();
        foreach(ToolMode mode in ToolModes) {
            if(mode.ShouldDisplay(this, slot, forPlayer, blockSel)) skillItems.Add(mode.GetDisplaySkillItem(api));
        }
        return skillItems.Any() ? skillItems.ToArray() : null;
    }

    public override int GetToolMode(ItemSlot slot, IPlayer byPlayer, BlockSelection blockSelection)
    {
        AssetLocation modeCode = ToolModeAPI.GetItemToolMode(byPlayer, this);
        if(modeCode == null) return 0;
        for(int i = 0; i < ToolModes.Length; i++) {
            if(ToolModes[i].Code == modeCode) return i;
        }
        return 0;
    }

    public ToolMode GetRealToolMode(IPlayer byPlayer)
    {
        return ToolModes[GetToolMode(null, byPlayer, null)];
    }

    public override void SetToolMode(ItemSlot slot, IPlayer byPlayer, BlockSelection blockSelection, int selection)
    {
        if(byPlayer.Entity.Api.Side == EnumAppSide.Server) return;
        
        SkillItem[] visibleItems = GetToolModes(slot, byPlayer as IClientPlayer, blockSelection);
        if(visibleItems == null) return;

        ToolMode selectedMode = ToolModes.First(mode => mode.Code == visibleItems[selection].Code);
        ToolModeAPI.SetItemToolMode(byPlayer, this, selectedMode);
    }

    public override bool OnBlockBrokenWith(IWorldAccessor world, Entity byEntity, ItemSlot itemslot, BlockSelection blockSel, float dropQuantityMultiplier = 1)
    {
        IPlayer player = (byEntity as EntityPlayer)?.Player;
        ToolMode currentMode = ToolModes.GetValue(GetToolMode(itemslot, player, blockSel)) as ToolMode;
        if(currentMode == null) {
            return base.OnBlockBrokenWith(world, byEntity, itemslot, blockSel, dropQuantityMultiplier);
        }

        EnumHandling handling = EnumHandling.PassThrough;
        bool modeResult = currentMode.OnBlockBrokenWith(world, byEntity, itemslot, blockSel, dropQuantityMultiplier, ref handling);
        if(handling != EnumHandling.PassThrough) {
            return modeResult;
        }

        return base.OnBlockBrokenWith(world, byEntity, itemslot, blockSel, dropQuantityMultiplier);
    }

    public override float OnBlockBreaking(IPlayer player, BlockSelection blockSel, ItemSlot itemslot, float remainingResistance, float dt, int counter)
    {
        ToolMode currentMode = ToolModes.GetValue(GetToolMode(itemslot, player, blockSel)) as ToolMode;
        if(currentMode == null) {
            return base.OnBlockBreaking(player, blockSel, itemslot, remainingResistance, dt, counter);
        }

        EnumHandling handling = EnumHandling.PassThrough;
        float result = currentMode.OnBlockBreaking(player, blockSel, itemslot, remainingResistance, dt, counter, ref handling);
        if(handling != EnumHandling.PassThrough) remainingResistance = result;
        if(handling == EnumHandling.PreventSubsequent) return result;

        return base.OnBlockBreaking(player, blockSel, itemslot, remainingResistance, dt, counter);
    }

    public override void OnHeldAttackStart(ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, ref EnumHandHandling handHandling)
    {
        IPlayer player = (byEntity as EntityPlayer)?.Player;
        ToolMode mode = ToolModes.GetValue(GetToolMode(slot, player, blockSel)) as ToolMode;
        if(mode == null) {
            base.OnHeldAttackStart(slot, byEntity, blockSel, entitySel, ref handHandling);
            return;
        }

        EnumHandling handling = EnumHandling.PassThrough;
        mode.OnHeldAttackStart(slot, byEntity, blockSel, entitySel, ref handHandling, ref handling);
        
        if(handling == EnumHandling.PreventSubsequent) return;
        if(handling != EnumHandling.PreventDefault) {
            if(HeldSounds?.Attack != null && api.World.Side == EnumAppSide.Client) {
                api.World.PlaySoundAt(HeldSounds.Attack, 0, 0, 0, null, 0.9f + (float)api.World.Rand.NextDouble());
            }
        }

        base.OnHeldAttackStart(slot, byEntity, blockSel, entitySel, ref handHandling);
        return;
    }

    public override bool OnHeldAttackCancel(float secondsPassed, ItemSlot slot, EntityAgent byEntity, BlockSelection blockSelection, EntitySelection entitySel, EnumItemUseCancelReason cancelReason)
    {
        IPlayer player = (byEntity as EntityPlayer)?.Player;
        ToolMode mode = ToolModes.GetValue(GetToolMode(slot, player, blockSelection)) as ToolMode;
        if(mode == null) {
            return base.OnHeldAttackCancel(secondsPassed, slot, byEntity, blockSelection, entitySel, cancelReason);
        }

        EnumHandling handling = EnumHandling.PassThrough;
        bool tmResult = mode.OnHeldAttackCancel(secondsPassed, slot, byEntity, blockSelection, entitySel, cancelReason, ref handling);
        
        if(handling != EnumHandling.PassThrough) {
            return tmResult;
        }

        return base.OnHeldAttackCancel(secondsPassed, slot, byEntity, blockSelection, entitySel, cancelReason);
    }

    public override bool OnHeldAttackStep(float secondsPassed, ItemSlot slot, EntityAgent byEntity, BlockSelection blockSelection, EntitySelection entitySel)
    {
        IPlayer player = (byEntity as EntityPlayer)?.Player;
        ToolMode mode = ToolModes.GetValue(GetToolMode(slot, player, blockSelection)) as ToolMode;
        if(mode == null) return base.OnHeldAttackStep(secondsPassed, slot, byEntity, blockSelection, entitySel);

        EnumHandling handling = EnumHandling.PassThrough;
        bool tmResult = mode.OnHeldAttackStep(secondsPassed, slot, byEntity, blockSelection, entitySel, ref handling);
        
        if(handling != EnumHandling.PassThrough) {
            return tmResult;
        }

        return base.OnHeldAttackStep(secondsPassed, slot, byEntity, blockSelection, entitySel);
    }

    public override void OnHeldAttackStop(float secondsPassed, ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel)
    {
        IPlayer player = (byEntity as EntityPlayer)?.Player;
        ToolMode mode = ToolModes.GetValue(GetToolMode(slot, player, blockSel)) as ToolMode;
        if(mode == null) {
            base.OnHeldAttackStop(secondsPassed, slot, byEntity, blockSel, entitySel);
            return;
        }

        EnumHandling handling = EnumHandling.PassThrough;
        mode.OnHeldAttackStop(secondsPassed, slot, byEntity, blockSel, entitySel, ref handling);

        if(handling == EnumHandling.PreventSubsequent) return;
        base.OnHeldAttackStop(secondsPassed, slot, byEntity, blockSel, entitySel);
    }

    public override void OnHeldInteractStart(ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, bool firstEvent, ref EnumHandHandling handling)
    {
        IPlayer player = (byEntity as EntityPlayer)?.Player;
        ToolMode mode = ToolModes.GetValue(GetToolMode(slot, player, blockSel)) as ToolMode;
        if(mode == null) {
            base.OnHeldInteractStart(slot, byEntity, blockSel, entitySel, firstEvent, ref handling);
            return;
        }

        EnumHandling tmHandling = EnumHandling.PassThrough;
        mode.OnHeldInteractStart(slot, byEntity, blockSel, entitySel, firstEvent, ref handling, ref tmHandling);

        if(tmHandling == EnumHandling.PreventSubsequent) return;
        base.OnHeldInteractStart(slot, byEntity, blockSel, entitySel, firstEvent, ref handling);
    }

    public override bool OnHeldInteractStep(float secondsUsed, ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel)
    {
        IPlayer player = (byEntity as EntityPlayer)?.Player;
        ToolMode mode = ToolModes.GetValue(GetToolMode(slot, player, blockSel)) as ToolMode;
        if(mode == null) {
            return base.OnHeldInteractStep(secondsUsed, slot, byEntity, blockSel, entitySel);
        }

        EnumHandling handling = EnumHandling.PassThrough;
        bool result = mode.OnHeldInteractStep(secondsUsed, slot, byEntity, blockSel, entitySel, ref handling);

        if(handling == EnumHandling.PreventSubsequent) return result;
        return base.OnHeldInteractStep(secondsUsed, slot, byEntity, blockSel, entitySel);
    }

    public override bool OnHeldInteractCancel(float secondsUsed, ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, EnumItemUseCancelReason cancelReason)
    {
        IPlayer player = (byEntity as EntityPlayer)?.Player;
        ToolMode mode = ToolModes.GetValue(GetToolMode(slot, player, blockSel)) as ToolMode;
        if(mode == null) {
            return base.OnHeldInteractCancel(secondsUsed, slot, byEntity, blockSel, entitySel, cancelReason);
        }

        EnumHandling handling = EnumHandling.PassThrough;
        bool result = mode.OnHeldInteractCancel(secondsUsed, slot, byEntity, blockSel, entitySel, cancelReason, ref handling);

        if(handling == EnumHandling.PreventSubsequent) return result;
        return base.OnHeldInteractCancel(secondsUsed, slot, byEntity, blockSel, entitySel, cancelReason);
    }

    public override void OnHeldInteractStop(float secondsUsed, ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel)
    {
        IPlayer player = (byEntity as EntityPlayer)?.Player;
        ToolMode mode = ToolModes.GetValue(GetToolMode(slot, player, blockSel)) as ToolMode;
        if(mode == null) {
            base.OnHeldInteractStop(secondsUsed, slot, byEntity, blockSel, entitySel);
            return;
        }

        EnumHandling handling = EnumHandling.PassThrough;
        mode.OnHeldInteractStop(secondsUsed, slot, byEntity, blockSel, entitySel, ref handling);

        if(handling == EnumHandling.PreventSubsequent) return;
        base.OnHeldInteractStop(secondsUsed, slot, byEntity, blockSel, entitySel);
    }
}