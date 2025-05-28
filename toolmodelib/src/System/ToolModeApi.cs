using System;
using System.Collections.Generic;
using System.ComponentModel.Design;
using System.Linq;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.GameContent;

namespace ToolModeLib;

public static class ToolModeAPI
{
    /// <summary>
    /// Gets the ToolModeRegistry mod system, useful for manually requesting client-server syncs.
    /// </summary>
    /// <param name="api"></param>
    /// <returns></returns>
    public static ToolModeRegistry GetToolModeRegistry(this ICoreAPI api)
    {
        return api.ModLoader.GetModSystem<ToolModeRegistry>();
    }

    /// <summary>
    /// Register a new tool mode type mapping.
    /// Must be called before any blocks or items are registered.
    /// Be sure to call this on both client and server sides.
    /// </summary>
    /// <param name="api"></param>
    /// <param name="code">identifier for this tool mode.</param>
    /// <param name="type">type to instantiate.</param>
    public static void RegisterToolMode(this ICoreAPI api, AssetLocation code, Type type)
    {
        api.GetToolModeRegistry().RegisterToolMode(code, type);
    }

    /// <summary>
    /// Register a new tool mode group.
    /// Groups are automatically applied to items with a Group accessor that returns a matching code.
    /// Must be called before any blocks or items are registered.
    /// Be sure to call this on both client and server sides.
    /// </summary>
    /// <param name="api"></param>
    /// <param name="code">identifier for this mode group.</param>
    /// <param name="types">list of identifiers for this group to instantiate.</param>
    public static void RegisterToolModeGroup(this ICoreAPI api, AssetLocation code, params AssetLocation[] types)
    {
        api.GetToolModeRegistry().RegisterModeGroup(code, types);
    }

    public static ToolMode[] GetToolModeObjs(this CollectibleObject collObj, ICoreAPI api)
    {
        return api.GetToolModeRegistry().GetCollectibleToolModes(collObj);
    }

    public static ToolMode GetToolModeObj(this CollectibleObject collObj, ICoreAPI api, IPlayer forPlayer)
    {
        ToolMode[] toolModes = GetToolModeObjs(collObj, api);
        if (toolModes == null) return null;

        AssetLocation modeCode = GetCollectibleToolMode(forPlayer, collObj);
        return toolModes.FirstOrDefault(mode => mode.Code == modeCode);
    }

    public static AssetLocation GetToolModeGroup(this CollectibleObject collObj, ICoreAPI api)
    {
        return api.GetToolModeRegistry().GetCollectibleGroup(collObj);
    }

    internal const string KEY_GROUPTOOLMODETREE = "toolmodelib:group";
    internal const string KEY_ITEMTOOLMODETREE = "toolmodelib:item";
    internal const string KEY_BLOCKTOOLMODETREE = "toolmodelib:block";

    internal static ITreeAttribute GetOrCreateToolModeAttrib(IPlayer forPlayer, string key)
    {
        return forPlayer.Entity.Attributes.GetOrAddTreeAttribute(key);
    }

    internal static AssetLocation GetTreeToolMode(IPlayer forPlayer, AssetLocation key, string treeKey)
    {
        ITreeAttribute groupTree = GetOrCreateToolModeAttrib(forPlayer, treeKey);
        if(groupTree.HasAttribute(key)) return AssetLocation.Create(groupTree.GetAsString(key));
        return null;
    }

    /// <summary>
    /// Returns the tool mode identifier for the given group.
    /// NULL if the group has no mode selected.
    /// </summary>
    /// <param name="forPlayer">player to get the tool mode for.</param>
    /// <param name="group">group identifier to get the tool mode for.</param>
    /// <returns>tool mode identifier.</returns>
    public static AssetLocation GetGroupToolMode(IPlayer forPlayer, AssetLocation group)
    { return GetTreeToolMode(forPlayer, group, KEY_GROUPTOOLMODETREE); }
    
    public static AssetLocation GetCollectibleToolMode(IPlayer forPlayer, CollectibleObject collObj)
    {
        return collObj.ItemClass switch
        {
            EnumItemClass.Block => throw new NotImplementedException(),
            EnumItemClass.Item => GetItemToolMode(forPlayer, collObj as Item)
        };
    }

    /// <summary>
    /// Returns the tool mode identifier for the given item.
    /// If the item is a part of a group, this method prioritizes the item-specific tool modes.
    /// NULL if the item has no mode selected.
    /// </summary>
    /// <param name="forPlayer">player to get the tool mode for.</param>
    /// <param name="item">item to get the tool mode for.</param>
    /// <returns>tool mode identifier.</returns>
    public static AssetLocation GetItemToolMode(IPlayer forPlayer, Item item)
    {
        AssetLocation itemMode = GetTreeToolMode(forPlayer, item.Code, KEY_ITEMTOOLMODETREE);
        AssetLocation itemGroup = item.GetToolModeGroup(forPlayer.Entity.Api);
        if (itemGroup == null) return itemMode;

        AssetLocation groupMode = GetGroupToolMode(forPlayer, itemGroup);
        if (itemMode == null) return groupMode;
        return itemMode;
    }

    public static AssetLocation GetBlockToolMode(IPlayer forPlayer, Block block)
    {
        AssetLocation blockGroup = block.GetToolModeGroup(forPlayer.Entity.Api);
        AssetLocation blockMode = GetTreeToolMode(forPlayer, block.Code, KEY_BLOCKTOOLMODETREE);
        if (blockGroup == null) return blockMode;

        AssetLocation groupMode = GetGroupToolMode(forPlayer, blockGroup);
        if (groupMode != null) return groupMode;
        return blockMode;
    }

    internal static void SetTreeToolMode(IPlayer byPlayer, string treeKey, AssetLocation key, AssetLocation mode)
    {
        if(mode == null) {
            GetOrCreateToolModeAttrib(byPlayer, treeKey).RemoveAttribute(key);
        }else{
            GetOrCreateToolModeAttrib(byPlayer, treeKey).SetString(key, mode);
        }
        if(byPlayer.Entity.Api.Side == EnumAppSide.Client) {
            byPlayer.Entity.Api.GetToolModeRegistry().SendSyncPacket(treeKey, key, mode);
        }
    }

    internal static void SetTreeToolMode(IPlayer byPlayer, string treeKey, AssetLocation key, ToolMode mode)
    {
        if(mode == null) {
            GetOrCreateToolModeAttrib(byPlayer, treeKey).RemoveAttribute(key);
        }else{
            GetOrCreateToolModeAttrib(byPlayer, treeKey).SetString(key, mode.Code);
        }
        if(byPlayer.Entity.Api.Side == EnumAppSide.Client) {
            byPlayer.Entity.Api.GetToolModeRegistry().SendSyncPacket(treeKey, key, mode?.Code);
        }
    }

    /// <summary>
    /// Sets the tool mode for a group given a tool mode instance.
    /// </summary>
    /// <param name="byPlayer">player to set the tool mode for.</param>
    /// <param name="group">group to set the tool mode for.</param>
    /// <param name="mode">tool mode to set to.</param>
    public static void SetGroupToolMode(IPlayer byPlayer, AssetLocation group, ToolMode mode)
    {
        SetTreeToolMode(byPlayer, KEY_GROUPTOOLMODETREE, group, mode);
    }

    public static void SetCollectibleToolMode(IPlayer byPlayer, CollectibleObject collObj, ToolMode mode)
    {
        switch (collObj.ItemClass)
        {
            case EnumItemClass.Block: throw new NotImplementedException();
            case EnumItemClass.Item: SetItemToolMode(byPlayer, collObj as Item, mode); break;
        }
    }

    /// <summary>
    /// Sets the tool mode for an item given a tool mode instance.
    /// If the passed mode has a group that matches the item's group, sets the group tool mode instead.
    /// </summary>
    /// <param name="byPlayer">player to set the tool mode for.</param>
    /// <param name="item">item to set the tool mode for.</param>
    /// <param name="mode">tool mode to set to.</param>
    public static void SetItemToolMode(IPlayer byPlayer, Item item, ToolMode mode)
    {
        AssetLocation itemGroup = item.GetToolModeGroup(byPlayer.Entity.Api);
        if (itemGroup != null && itemGroup == mode?.Group)
        {
            SetGroupToolMode(byPlayer, itemGroup, mode);
            SetTreeToolMode(byPlayer, KEY_ITEMTOOLMODETREE, item.Code, null as ToolMode);
        }
        else
        {
            SetTreeToolMode(byPlayer, KEY_ITEMTOOLMODETREE, item.Code, mode);
        }
    }

    public static void SetBlockToolMode(IPlayer byPlayer, Block block, ToolMode mode)
    {
        AssetLocation blockGroup = block.GetToolModeGroup(byPlayer.Entity.Api);
        if(mode != null && mode.Group != block.Code) {
            SetGroupToolMode(byPlayer, mode.Group, mode);
        }else if(blockGroup != null){
            SetGroupToolMode(byPlayer, blockGroup, null);
        }
        SetTreeToolMode(byPlayer, KEY_BLOCKTOOLMODETREE, block.Code, mode);
    }
}