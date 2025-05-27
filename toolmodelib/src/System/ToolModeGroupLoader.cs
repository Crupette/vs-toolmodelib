using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;
using System.Threading;
using Newtonsoft.Json.Linq;
using Vintagestory.API.Common;
using Vintagestory.API.Util;

namespace ToolModeLib;

public class ToolModeGroupJson
{
    public AssetLocation code;
    public AssetLocation[] members;
}

public class ToolModeLoader : ModSystem
{
    public static AssetCategory toolmodegroups = new("toolmodegroups", AffectsGameplay: true, EnumAppSide.Universal);
    public override double ExecuteOrder() => 0.1;

    public override void AssetsLoaded(ICoreAPI api)
    {
        base.AssetsLoaded(api);

        api.Assets.Reload(toolmodegroups);
        Dictionary<AssetLocation, JToken> toolModeGroups = api.Assets.GetMany<JToken>(api.Logger, "toolmodegroups");
        toolModeGroups.Foreach((v) => LoadToolModeGroup(api, v.Key, v.Value));
    }

    void LoadToolModeGroup(ICoreAPI api, AssetLocation path, JToken token)
    {
        AssetLocation groupCode = AssetLocation.Create(token["code"].ToString(), path.Domain);
        AssetLocation[] groupMembers = token["members"].Select(v => AssetLocation.Create(v.ToString(), path.Domain)).ToArray();

        api.RegisterToolModeGroup(groupCode, groupMembers);
    }
}