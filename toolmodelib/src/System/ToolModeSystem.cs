using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using HarmonyLib;
using ProtoBuf;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Server;
using Vintagestory.API.Util;

namespace ToolModeLib;

public class ToolModeRegistry : ModSystem
{
    Dictionary<AssetLocation, List<AssetLocation>> groupModes = new();
    Dictionary<AssetLocation, Type> CodeToTypeMapping = new();

    Dictionary<int, ToolMode[]> ItemToolModes = new();
    Dictionary<int, AssetLocation> ItemGroups = new();

    Harmony harmony;

    public override void Start(ICoreAPI api)
    {
        base.Start(api);

        api.Network.RegisterChannel("toolmodelib:syncmode").RegisterMessageType<ToolModeSyncPacket>();

        api.RegisterItemClass("toolmodelib:ItemClayWithModes", typeof(Content.ItemClayWithModes));
        api.RegisterToolMode("game:clay-1size", typeof(Content.ToolModeClay1Size));
        api.RegisterToolMode("game:clay-2size", typeof(Content.ToolModeClay2Size));
        api.RegisterToolMode("game:clay-3size", typeof(Content.ToolModeClay3Size));
        api.RegisterToolMode("game:clay-duplicate", typeof(Content.ToolModeClayDuplicate));

        api.RegisterItemClass("toolmodelib:ItemHammerWithModes", typeof(Content.ItemHammerWithModes));
        api.RegisterToolMode("game:hammer-hit", typeof(Content.ToolModeHammerHit));
        api.RegisterToolMode("game:hammer-upset-up", typeof(Content.ToolModeHammerUpsetUp));
        api.RegisterToolMode("game:hammer-upset-right", typeof(Content.ToolModeHammerUpsetRight));
        api.RegisterToolMode("game:hammer-upset-down", typeof(Content.ToolModeHammerUpsetDown));
        api.RegisterToolMode("game:hammer-upset-left", typeof(Content.ToolModeHammerUpsetLeft));
        api.RegisterToolMode("game:hammer-split", typeof(Content.ToolModeHammerSplit));

        api.RegisterItemClass("toolmodelib:ItemScytheWithModes", typeof(Content.ItemScytheWithModes));
        api.RegisterToolMode("game:scythe-trim", typeof(Content.ToolModeScytheTrim));
        api.RegisterToolMode("game:scythe-remove", typeof(Content.ToolModeScytheRemove));

        if(!Harmony.HasAnyPatches(Mod.Info.ModID)) {
            harmony = new Harmony(Mod.Info.ModID);
            harmony.PatchAll();
        }
    }

    public override void Dispose()
    {
        harmony?.UnpatchAll(Mod.Info.ModID);

        groupModes = new();
        CodeToTypeMapping = new();
    }

    public void RegisterToolMode(AssetLocation code, Type type)
    {
        CodeToTypeMapping.Add(code, type);
    }

    public void RegisterModeGroup(AssetLocation code, AssetLocation[] types)
    {
        if(!groupModes.TryGetValue(code, out var groupModeList)) {
            groupModeList = new List<AssetLocation>();
            groupModes.Add(code, groupModeList);
            groupModeList = groupModes[code];
        }
        foreach(var typeCode in types) {
            groupModeList.Add(typeCode);
        }
    }

    internal ToolMode CreateToolMode(AssetLocation groupCode, Type type)
    {
        try {
            ToolMode mode = Activator.CreateInstance(type, groupCode) as ToolMode;
            return mode;
        }catch(Exception e){
            throw new Exception($"Error on instantiating tool mode for '{groupCode}':\n{e}");
        }
    }

    public ToolMode CreateToolMode(CollectibleObject collectible, AssetLocation code)
    {
        if(!CodeToTypeMapping.TryGetValue(code, out var type)) {
            throw new Exception("Don't know how to instantiate tool mode of class '" + code + "'. Did you forget to register a mapping?");
        }
        ToolMode mode = CreateToolMode(collectible.Code, type);
        mode.Code = code.Clone();
        return mode;
    }

    public ToolMode CreateToolMode(AssetLocation group, AssetLocation code)
    {
        if(!CodeToTypeMapping.TryGetValue(code, out var type)) {
            throw new Exception("Don't know how to instantiate tool mode of class '" + code + "'. Did you forget to register a mapping?");
        }
        ToolMode mode = CreateToolMode(group, type);
        mode.Code = code.Clone();
        return mode;
    }

    public void SetCollectibleToolModes(CollectibleObject collObj, ToolMode[] modes)
    {
        switch (collObj.ItemClass)
        {
            case EnumItemClass.Block: throw new NotImplementedException(); break;
            case EnumItemClass.Item: ItemToolModes.Add(collObj.Id, modes); break;
        }
    }

    public void SetCollectibleGroup(CollectibleObject collObj, AssetLocation group)
    {
        switch (collObj.ItemClass)
        {
            case EnumItemClass.Block: throw new NotImplementedException();
            case EnumItemClass.Item: ItemGroups.Add(collObj.Id, group); break;
        }
    }

    public ToolMode[] GetCollectibleToolModes(CollectibleObject collObj)
    {
        return collObj.ItemClass switch
        {
            EnumItemClass.Block => null,
            EnumItemClass.Item => ItemToolModes.GetValueOrDefault(collObj.Id)
        };
    }

    public AssetLocation GetCollectibleGroup(CollectibleObject collObj)
    {
        AssetLocation group;
        switch (collObj.ItemClass)
        {
            case EnumItemClass.Block:
                throw new NotImplementedException();
            case EnumItemClass.Item:
                if (!ItemGroups.TryGetValue(collObj.Id, out group)) return null;
                return group;
        }
        return null;
    }

    internal void UnloadCollectibleToolModes(ICoreAPI api, CollectibleObject collObj)
    {
        ToolMode[] modes = null;
        switch (collObj.ItemClass)
        {
            case EnumItemClass.Block:
                break;
            case EnumItemClass.Item:
                if (!ItemToolModes.TryGetValue(collObj.Id, out modes)) return;
                modes.Foreach(mode => mode.OnUnloaded(api));
                ItemToolModes.Remove(collObj.Id);
                break;
        }
    }

    public ToolMode[] CreateToolModeGroup(AssetLocation group)
    {
        if (!groupModes.TryGetValue(group, out var groupTypes))
        {
            throw new Exception($"Don't know how to instantiate tool mode group of code '{group}'. Did you forget to register this group?");
        }
        List<ToolMode> newModes = new();
        foreach (var typeCode in groupTypes)
        {
            newModes.Add(CreateToolMode(group, typeCode));
        }
        return newModes.ToArray();
    }
    
    #region Server
    ICoreServerAPI sapi;
    IServerNetworkChannel serverSyncChannel;
    public override void StartServerSide(ICoreServerAPI api)
    {
        sapi = api;
        serverSyncChannel = sapi.Network.GetChannel("toolmodelib:syncmode").SetMessageHandler<ToolModeSyncPacket>((byPlayer, packet) => {
            ToolModeAPI.SetTreeToolMode(byPlayer, packet.tree, packet.key, packet.value);
        });
    }
    #endregion

    #region Client
    ICoreClientAPI capi;
    IClientNetworkChannel clientSyncChannel;
    public override void StartClientSide(ICoreClientAPI api)
    {
        capi = api;
        clientSyncChannel = capi.Network.GetChannel("toolmodelib:syncmode");
    }

    public void SendSyncPacket(string forTree, AssetLocation key, AssetLocation value)
    {
        clientSyncChannel.SendPacket<ToolModeSyncPacket>(new() { 
            tree = forTree,
            key = key,
            value = value
        });
    }
    #endregion
}

[ProtoContract]
internal class ToolModeSyncPacket
{
    [ProtoMember(1)]
    public string tree;
    [ProtoMember(2)]
    public AssetLocation key;
    [ProtoMember(3)]
    public AssetLocation value;
}