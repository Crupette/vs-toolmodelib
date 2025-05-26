using System;
using System.Linq;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;
using Vintagestory.GameContent;

namespace ToolModeLib.Content;

public class ItemScytheWithModes : ItemMultiBreakWithModes
{
    public override AssetLocation Group => "game:item-scythe";

    string[] allowedPrefixes;
    string[] disallowedSuffixes;

    public override void OnLoaded(ICoreAPI api)
    {
        base.OnLoaded(api);

        allowedPrefixes = Attributes["codePrefixes"].AsArray<string>();
        disallowedSuffixes = Attributes["disallowedSuffixes"].AsArray<string>();
    }

    public override bool CanMultiBreak(Block block) {
        foreach(var prefix in allowedPrefixes) {
            if(!block.Code.PathStartsWith(prefix)) continue;
            if(disallowedSuffixes == null) return true;

            foreach(var suffix in disallowedSuffixes) {
                if(block.Code.Path.EndsWithOrdinal(suffix)) return false;
            }
            return true;
        }
        return false;
    }

}

public abstract class ToolModeScythe : ToolModeMultiBreak
{
    public override int MultiBreakQuantity { get { return 5; } }
    public ToolModeScythe(AssetLocation group) : base(group)
    { }

    public override void OnHeldAttackStart(ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, ref EnumHandHandling handHandling, ref EnumHandling handling)
    {
        if(blockSel == null) return;
        IPlayer byPlayer = (byEntity as EntityPlayer)?.Player;
        
        if(byPlayer == null) return;
        if(!byEntity.World.Claims.TryAccess(byPlayer, blockSel.Position, EnumBlockAccessFlags.BuildOrBreak)) return;

        byEntity.Attributes.SetBool("didBreakBlocks", false);
        byEntity.Attributes.SetBool("didPlayScytheSound", false);
        handling = EnumHandling.PreventSubsequent;
        handHandling = EnumHandHandling.PreventDefault;
    }

    public override bool OnHeldAttackStep(float secondsPassed, ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, ref EnumHandling handling)
    {
        if(byEntity.World.Side == EnumAppSide.Client) {
            ModelTransform tf = new ModelTransform();
            tf.EnsureDefaultValues();

            float t = secondsPassed / 1.35f;

            float f = (float)Easings.EaseOutBack(Math.Min(t * 2f, 1));
            float f2 = (float)Math.Sin(GameMath.Clamp(Math.PI * 1.4f * (t - 0.5f), 0, 3));

            tf.Translation.X += Math.Min(0.2f, t * 3);
            tf.Translation.Y -= Math.Min(0.75f, t * 3);
            tf.Translation.Z -= Math.Min(1, t * 3);
            tf.ScaleXYZ += Math.Min(1, t * 3);
            tf.Origin.X -= Math.Min(0.75f, t * 3);
            tf.Rotation.X = -Math.Min(30, t * 30) + f * 30 + (float)f2 * 120f;
            tf.Rotation.Z = -f * 110;

            if (secondsPassed > 1.75f)
            {
                float b = 2 * (secondsPassed - 1.75f);
                tf.Rotation.Z += b * 140;
                tf.Rotation.X /= (1 + b * 10);
                tf.Translation.X -= b * 0.4f;
                tf.Translation.Y += b * 2 / 0.75f;
                tf.Translation.Z += b * 2;
            }

            byEntity.Controls.UsingHeldItemTransformBefore = tf;
        }
        PerformActions(secondsPassed, byEntity, slot, blockSel);
        handling = EnumHandling.PreventSubsequent;

        if(byEntity.World.Side == EnumAppSide.Server) return true;
        return secondsPassed < 2f;
    }

    public override void OnHeldAttackStop(float secondsPassed, ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, ref EnumHandling handling)
    {
        handling = EnumHandling.PreventSubsequent;
        PerformActions(secondsPassed, byEntity, slot, blockSel);
    }

    public void PerformActions(float secondsPassed, EntityAgent byEntity, ItemSlot slot, BlockSelection blockSel)
    {
        if(blockSel == null) return;

        Block block = byEntity.World.BlockAccessor.GetBlock(blockSel.Position);
        IPlayer byPlayer = (byEntity as EntityPlayer)?.Player;

        if(byPlayer == null) return;

        if (slot.Itemstack?.Item is not ItemMultiBreakWithModes itemMultiBreak) return;
        bool canMultiBreak = itemMultiBreak.CanMultiBreak(block);
        if(!canMultiBreak) return;

        if(secondsPassed > 0.75f && byEntity.Attributes.GetBool("didPlayScytheSound") == false) {
            byEntity.World.PlaySoundAt(new AssetLocation("sounds/tool/scythe1"), byEntity, byPlayer, true, 16);
            byEntity.Attributes.SetBool("didPlayScytheSound", true);
        }

        if(secondsPassed > 1.05f && byEntity.Attributes.GetBool("didBreakBlocks") == false)
        {
            if(byEntity.World.Side == EnumAppSide.Server && byEntity.World.Claims.TryAccess(byPlayer, blockSel.Position, EnumBlockAccessFlags.BuildOrBreak)) {
                itemMultiBreak.OnBlockBrokenWith(byEntity.World, byEntity, slot, blockSel);
            }
            byEntity.Attributes.SetBool("didBreakBlocks", true);
        }
    }
}

public class ToolModeScytheTrim : ToolModeScythe
{
    SkillItem skillItem;

    public ToolModeScytheTrim(AssetLocation group) : base(group)
    { }

    public override void OnLoaded(ICoreAPI api)
    {
        if(api is not ICoreClientAPI capi) return;
        skillItem = new SkillItem() {
            Code = Code,
            Name = Lang.Get("Trim grass")
        }.WithIcon(capi, capi.Gui.LoadSvgWithPadding(new AssetLocation("textures/icons/scythetrim.svg"), 48, 48, 5, ColorUtil.WhiteArgb));
        skillItem.TexturePremultipliedAlpha = false;
    }

    public override void OnUnloaded(ICoreAPI api)
    {
        skillItem?.Dispose();
    }

    public override SkillItem GetDisplaySkillItem(ICoreAPI api)
    {
        return skillItem;
    }

    public override void BreakMultiBlock(BlockPos pos, IPlayer plr)
    {
        IWorldAccessor world = plr.Entity.World;
        Block block = world.BlockAccessor.GetBlock(pos);

        Block trimmedBlock = world.GetBlock(block.CodeWithVariant("tallgrass", "eaten"));
        bool blockIsTallgrass = block.Variant.ContainsKey("tallgrass");

        if(blockIsTallgrass && block == trimmedBlock) return;
        if(blockIsTallgrass && trimmedBlock != null) {
            world.BlockAccessor.BreakBlock(pos, plr);
            world.BlockAccessor.MarkBlockDirty(pos);

            world.BlockAccessor.SetBlock(trimmedBlock.BlockId, pos);
            var be = world.BlockAccessor.GetBlockEntity(pos) as BlockEntityTransient;
            if(be != null) be.ConvertToOverride = block.Code.ToShortString();
            return;
        }
        base.BreakMultiBlock(pos, plr);
    }
}

public class ToolModeScytheRemove : ToolModeScythe
{
    SkillItem skillItem;

    public ToolModeScytheRemove(AssetLocation group) : base(group)
    { }

    public override void OnLoaded(ICoreAPI api)
    {
        if(api is not ICoreClientAPI capi) return;
        skillItem = new SkillItem() {
            Code = Code,
            Name = Lang.Get("Remove grass")
        }.WithIcon(capi, capi.Gui.LoadSvgWithPadding(new AssetLocation("textures/icons/scytheremove.svg"), 48, 48, 5, ColorUtil.WhiteArgb));
        skillItem.TexturePremultipliedAlpha = false;
    }

    public override void OnUnloaded(ICoreAPI api)
    {
        skillItem?.Dispose();
    }

    public override SkillItem GetDisplaySkillItem(ICoreAPI api)
    {
        return skillItem;
    }
}