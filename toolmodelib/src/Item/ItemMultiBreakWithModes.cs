using System;
using System.Linq;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;
using Vintagestory.GameContent;

namespace ToolModeLib.Content;

public class ItemMultiBreakWithModes : ItemWithModes
{
    public override AssetLocation Group => "game:item-scythe";

    public virtual bool CanMultiBreak(Block block) {
        return true;
    }

    public override float OnBlockBreaking(IPlayer player, BlockSelection blockSel, ItemSlot itemslot, float remainingResistance, float dt, int counter)
    {
        remainingResistance = base.OnBlockBreaking(player, blockSel, itemslot, remainingResistance, dt, counter);
        ToolMode mode = GetRealToolMode(player);
        if(mode is IActOnBlockBreakingAfter listener) {
            return listener.OnBlockBreakingAfter(player, blockSel, itemslot, remainingResistance, dt, counter);
        }
        return remainingResistance;
    }
}

public interface IActOnBlockBreakingAfter
{
    public float OnBlockBreakingAfter(IPlayer player, BlockSelection blockSel, ItemSlot itemSlot, float remainingResistance, float dt, int counter);
}

public abstract class ToolModeMultiBreak : ToolMode, IActOnBlockBreakingAfter
{
    public virtual int MultiBreakQuantity { get { return 5; } }
    public ToolModeMultiBreak(AssetLocation group) : base(group)
    { }

    public float OnBlockBreakingAfter(IPlayer player, BlockSelection blockSel, ItemSlot itemSlot, float remainingResistance, float dt, int counter)
    {
        int leftDurability = itemSlot.Itemstack.Collectible.GetRemainingDurability(itemSlot.Itemstack);
        DamageNearbyBlocks(player, itemSlot, blockSel, remainingResistance, leftDurability);
        return remainingResistance;
    }

    public void DamageNearbyBlocks(IPlayer player, ItemSlot itemSlot, BlockSelection blockSel, float damage, int leftDurability)
    {
        Block block = player.Entity.World.BlockAccessor.GetBlock(blockSel.Position);
        ItemMultiBreakWithModes itemMultiBreak = itemSlot.Itemstack?.Item as ItemMultiBreakWithModes;
        if(itemMultiBreak == null) return;

        if(!itemMultiBreak.CanMultiBreak(block)) return;
        Vec3d hitPos = blockSel.Position.ToVec3d().Add(blockSel.HitPosition);
        OrderedDictionary<BlockPos, float> dict = GetNearblyMultibreakables(player.Entity.World, blockSel.Position, hitPos, itemMultiBreak);
        var orderedPositions = dict.OrderBy(v => v.Value).Select(v => v.Key);

        int q = Math.Min(MultiBreakQuantity, leftDurability);
        foreach(var pos in orderedPositions) {
            if(q == 0) break;
            BlockFacing facing = BlockFacing.FromNormal(player.Entity.ServerPos.GetViewVector()).Opposite;
            if(!player.Entity.World.Claims.TryAccess(player, pos, EnumBlockAccessFlags.BuildOrBreak)) continue;

            player.Entity.World.BlockAccessor.DamageBlock(pos, facing, damage);
            q--;
        }
    }

    public override bool OnBlockBrokenWith(IWorldAccessor world, Entity byEntity, ItemSlot itemSlot, BlockSelection blockSel, float dropQuantityMultiplier, ref EnumHandling handling)
    {
        handling = EnumHandling.PreventSubsequent;
        Block block = world.BlockAccessor.GetBlock(blockSel.Position);

        IPlayer byPlayer = (byEntity as EntityPlayer)?.Player;
        if(byPlayer == null || itemSlot.Itemstack == null) return true;

        ItemMultiBreakWithModes itemMultiBreak = itemSlot.Itemstack.Item as ItemMultiBreakWithModes;
        if(itemMultiBreak == null) return true;

        BreakMultiBlock(blockSel.Position, byPlayer);

        if(!itemMultiBreak.CanMultiBreak(block)) return true;
        Vec3d hitPos = blockSel.Position.ToVec3d().Add(blockSel.HitPosition);
        var orderedPositions = GetNearblyMultibreakables(world, blockSel.Position, hitPos, itemMultiBreak).OrderBy(v => v.Value);

        int leftDurability = itemMultiBreak.GetRemainingDurability(itemSlot.Itemstack);
        int q = 0;

        foreach(var val in orderedPositions) {
            if(!byPlayer.Entity.World.Claims.TryAccess(byPlayer, val.Key, EnumBlockAccessFlags.BuildOrBreak)) continue;

            BreakMultiBlock(val.Key, byPlayer);
            itemMultiBreak.DamageItem(world, byEntity, itemSlot);
            
            q++;
            if(q >= MultiBreakQuantity || itemSlot.Itemstack == null) break;
        }
        return true;
    }

    public virtual void BreakMultiBlock(BlockPos pos, IPlayer plr)
    {
        IWorldAccessor world = plr.Entity.World;
        world.BlockAccessor.BreakBlock(pos, plr);
        world.BlockAccessor.MarkBlockDirty(pos);
    }

    OrderedDictionary<BlockPos, float> GetNearblyMultibreakables(IWorldAccessor world, BlockPos pos, Vec3d hitPos, ItemMultiBreakWithModes itemMultiBreak)
    {
        OrderedDictionary<BlockPos, float> positions = new OrderedDictionary<BlockPos, float>();
        
        for (int dx = -1; dx <= 1; dx++)
        {
            for (int dy = -1; dy <= 1; dy++)
            {
                for (int dz = -1; dz <= 1; dz++)
                {
                    if (dx == 0 && dy == 0 && dz == 0) continue;

                    BlockPos dpos = pos.AddCopy(dx, dy, dz);
                    if (itemMultiBreak.CanMultiBreak(world.BlockAccessor.GetBlock(dpos)))
                    {
                        positions.Add(dpos, hitPos.SquareDistanceTo(dpos.X + 0.5, dpos.Y + 0.5, dpos.Z + 0.5));
                    }
                }
            }
        }

        return positions;
    }
}