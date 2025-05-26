using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.Util;

namespace ToolModeLib;

[Obsolete(message:"Implementation is not yet done, since no vanilla block needs tool modes.")]
public abstract class BlockWithModes : Block, ICollectibleWithModes
{
    public ToolMode[] ToolModes = Array.Empty<ToolMode>();

    public virtual AssetLocation Group => null;

    public override void OnLoaded(ICoreAPI api)
    {
        base.OnLoaded(api);
        if(Group != null)
            ToolModes = api.GetToolModeRegistry().CreateToolModeGroup(Group);

        JsonObject[] toolModesArray = Attributes["toolmodes"]?.ToArray();
        List<ToolMode> itemSpecificModes = new();
        if(toolModesArray.Any()) {
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

    public override SkillItem[] GetToolModes(ItemSlot slot, IClientPlayer forPlayer, BlockSelection blockSel)
    {
        List<SkillItem> skillItems = new();
        foreach(ToolMode mode in ToolModes) {
            if(mode.ShouldDisplay(this, slot, forPlayer, blockSel)) skillItems.Add(mode.GetDisplaySkillItem(api));
        }
        return skillItems.ToArray();
    }

    public override int GetToolMode(ItemSlot slot, IPlayer byPlayer, BlockSelection blockSelection)
    {
        return base.GetToolMode(slot, byPlayer, blockSelection);
    }
}