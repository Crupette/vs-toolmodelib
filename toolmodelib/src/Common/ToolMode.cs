using System;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;

namespace ToolModeLib;

public abstract class ToolMode
{
    /// <summary>
    /// Identifier for this tool mode set at instantiation.
    /// </summary>
    public AssetLocation Code;

    /// <summary>
    /// Group or item code this tool mode belongs to.
    /// Set by constructor.
    /// </summary>
    public AssetLocation Group;
    public string propertiesAsString;

    public ToolMode(AssetLocation group) {
        this.Group = group;
    }

    public virtual void Initialize(JsonObject properties)
    {
        propertiesAsString = properties.ToString();
    }

    public virtual void OnLoaded(ICoreAPI api) { }
    public virtual void OnUnloaded(ICoreAPI api) { }
    public virtual bool ShouldDisplay(CollectibleObject collObj, ItemSlot slot, IClientPlayer forPlayer, BlockSelection blockSel) { return true; }

    public abstract SkillItem GetDisplaySkillItem(ICoreAPI api);

    public virtual void OnHeldAttackStart(ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, ref EnumHandHandling handHandling, ref EnumHandling handling)
    { handling = EnumHandling.PassThrough; }

    public virtual bool OnHeldAttackCancel(float secondsPassed, ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, EnumItemUseCancelReason cancelReason, ref EnumHandling handling)
    { handling = EnumHandling.PassThrough; return false; }

    public virtual bool OnHeldAttackStep(float secondsPassed, ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, ref EnumHandling handling)
    { handling = EnumHandling.PassThrough; return false; }
    
    public virtual void OnHeldAttackStop(float secondsPassed, ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, ref EnumHandling handling)
    { handling = EnumHandling.PassThrough; }

    public virtual float OnBlockBreaking(IPlayer player, BlockSelection blockSel, ItemSlot itemslot, float remainingResistance, float dt, int counter, ref EnumHandling handling)
    { handling = EnumHandling.PassThrough; return remainingResistance; }

    public virtual bool OnBlockBrokenWith(IWorldAccessor world, Entity byEntity, ItemSlot itemSlot, BlockSelection blockSel, float dropQuantityMultiplier, ref EnumHandling handling) 
    { return true; }

    public virtual void OnHeldInteractStart(ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, bool firstEvent, ref EnumHandHandling handHandling, ref EnumHandling handling)
    { handling = EnumHandling.PassThrough; }

    public virtual bool OnHeldInteractStep(float secondsUsed, ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, ref EnumHandling handling)
    { handling = EnumHandling.PassThrough; return true; }

    public virtual bool OnHeldInteractCancel(float secondsUsed, ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, EnumItemUseCancelReason cancelReason, ref EnumHandling handling)
    { handling = EnumHandling.PassThrough; return true; }

    public virtual void OnHeldInteractStop(float secondsUsed, ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, ref EnumHandling handling)
    { handling = EnumHandling.PassThrough; }

    public virtual WorldInteraction[] GetHeldInteractionHelp(ItemSlot itemSlot)
    { return Array.Empty<WorldInteraction>(); }
}