using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Util;

namespace ToolModeLib.Patches;

[HarmonyPatch(typeof(CollectibleObject), nameof(CollectibleObject.OnLoadedNative))]
class CollectibleObjectPatch_OnLoadedNative
{
    static void Postfix(CollectibleObject __instance, ICoreAPI api)
    {
        if (__instance.Attributes == null) return;

        string domain = __instance.Code?.Domain ?? "game";
        ToolModeRegistry registry = api.GetToolModeRegistry();
        List<ToolMode> modes = new();

        if (__instance.Attributes.KeyExists("toolmodegroup"))
        {
            modes.AddRange(registry.CreateToolModeGroup(
                AssetLocation.Create(__instance.Attributes["toolmodegroup"].AsString(), domain)));
        }
        if (__instance.Attributes.KeyExists("toolmodes"))
        {
            string[] modeStrings = __instance.Attributes["toolmodes"].AsArray<string>();
            foreach (var mode in modeStrings)
            {
                modes.Add(registry.CreateToolMode(
                    __instance.Code, AssetLocation.Create(mode, domain)));
            }
        }

        if (!modes.Any()) return;
        foreach (var mode in modes)
        {
            mode.OnLoaded(api);
        }
        registry.SetCollectibleToolModes(__instance, modes.ToArray());
    }
}


[HarmonyPatch(typeof(CollectibleObject), nameof(CollectibleObject.OnUnloaded))]
class CollectibleObjectPatch_OnUnloaded
{
    static void Postfix(CollectibleObject __instance, ICoreAPI api)
    {
        api.GetToolModeRegistry().UnloadCollectibleToolModes(api, __instance);
    }
}

[HarmonyPatch(typeof(CollectibleObject), nameof(CollectibleObject.GetHeldInteractionHelp))]
class CollectibleObjectPatch_GetHeldInteractionHelp
{
    static void Postfix(CollectibleObject __instance, ref WorldInteraction[] __result, ItemSlot inSlot)
    {
        ICoreAPI api = AccessTools.Field(typeof(CollectibleObject), "api").GetValue(__instance) as ICoreAPI;
        ToolMode[] toolModes = __instance.GetToolModeObjs(api);
        if (toolModes != null && toolModes.Any())
        {
            __result = __result.Append(new WorldInteraction()
            {
                ActionLangCode = "heldhelp-settoolmode",
                HotKeyCode = "toolmodeselect"
            });
        }
    }
}

[HarmonyPatch(typeof(CollectibleObject), nameof(CollectibleObject.GetToolModes))]
class CollectibleObjectPatch_GetToolModes
{
    static bool Prefix(CollectibleObject __instance, ref SkillItem[] __result, ItemSlot slot, IClientPlayer forPlayer, BlockSelection blockSel)
    {
        __result = null;

        ICoreAPI api = forPlayer?.Entity.Api;
        if (api == null) return false;

        ToolMode[] toolModes = __instance.GetToolModeObjs(api);
        if (toolModes == null) return true;
        if (!toolModes.Any()) return true;

        List<SkillItem> skillItems = new();
        toolModes.Foreach(mode =>
        {
            if (mode.ShouldDisplay(__instance, slot, forPlayer, blockSel))
            {
                skillItems.Add(mode.GetDisplaySkillItem(api));
            }
        });
        if (!skillItems.Any())
        {
            __result = null;
            return false;
        }
        __result = skillItems.ToArray();
        return false;
    }
}

[HarmonyPatch(typeof(CollectibleObject), nameof(CollectibleObject.GetToolMode))]
class CollectibleObjectPatch_GetToolMode
{
    static bool Prefix(CollectibleObject __instance, ref int __result, ItemSlot slot, IPlayer byPlayer, BlockSelection blockSelection)
    {
        ICoreAPI api = byPlayer?.Entity.Api;
        if(api == null) return true;

        ToolMode[] toolModes = __instance.GetToolModeObjs(api);
        if (toolModes == null) return true;
        if (!toolModes.Any()) return true;

        __result = 0;
        AssetLocation modeCode = ToolModeAPI.GetCollectibleToolMode(byPlayer, __instance);
        if (modeCode == null) return false;

        for (int i = 0; i < toolModes.Length; i++)
        {
            if (toolModes[i].Code == modeCode)
            {
                __result = i;
                break;
            }
        }

        return false;
    }
}

[HarmonyPatch(typeof(CollectibleObject), nameof(CollectibleObject.SetToolMode))]
class CollectibleObjectPatch_SetToolMode
{
    static bool Prefix(CollectibleObject __instance, ItemSlot slot, IPlayer byPlayer, BlockSelection blockSelection, int toolMode)
    {
        ICoreAPI api = byPlayer?.Entity.Api;
        if (api == null) return true;

        ToolMode[] toolModes = __instance.GetToolModeObjs(api);
        if (toolModes == null) return true;
        if (!toolModes.Any()) return true;

        SkillItem[] visibleItems = __instance.GetToolModes(slot, byPlayer as IClientPlayer, blockSelection);
        if (visibleItems == null) return false;

        ToolMode selectedMode = toolModes.First(mode => mode.Code == visibleItems[toolMode].Code);
        ToolModeAPI.SetCollectibleToolMode(byPlayer, __instance, selectedMode);
        return false;
    }
}

[HarmonyPatch(typeof(CollectibleObject), nameof(CollectibleObject.OnBlockBreaking))]
class CollectibleObjectPatch_OnBlockBreaking
{
    static bool Prefix(CollectibleObject __instance, ref float __result, IPlayer player, BlockSelection blockSel, ItemSlot itemslot, float remainingResistance, float dt, int counter)
    {
        ICoreAPI api = player?.Entity.Api;
        if (api == null) return true;

        ToolMode currentMode = __instance.GetToolModeObj(api, player);
        if (currentMode == null) return true;

        EnumHandling tmHandling = EnumHandling.PassThrough;
        float tmResult = currentMode.OnBlockBreaking(player, blockSel, itemslot, remainingResistance, dt, counter, ref tmHandling);

        if (tmHandling == EnumHandling.PassThrough) return true;
        __result = tmResult;
        return false;
    }
}

[HarmonyPatch(typeof(CollectibleObject), nameof(CollectibleObject.OnBlockBrokenWith))]
class CollectibleObjectPatch_OnBlockBrokenWith
{
    static bool Prefix(CollectibleObject __instance, ref bool __result, IWorldAccessor world, Entity byEntity, ItemSlot itemslot, BlockSelection blockSel, float dropQuantityMultiplier)
    {
        ICoreAPI api = byEntity?.Api;
        if (api == null) return true;

        IPlayer player = (byEntity as EntityPlayer)?.Player;
        if (player == null) return true;

        ToolMode currentMode = __instance.GetToolModeObj(api, player);
        if (currentMode == null) return true;

        EnumHandling tmHandling = EnumHandling.PassThrough;
        bool tmResult = currentMode.OnBlockBrokenWith(world, byEntity, itemslot, blockSel, dropQuantityMultiplier, ref tmHandling);

        if (tmHandling == EnumHandling.PassThrough) return true;
        __result = tmResult;
        return false;
    }
}

[HarmonyPatch(typeof(CollectibleObject), nameof(CollectibleObject.OnHeldAttackStart))]
class CollectibleObjectPatch_OnHeldAttackStart
{
    static bool Prefix(CollectibleObject __instance, ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, ref EnumHandHandling handling)
    {
        ICoreAPI api = byEntity?.Api;
        if (api == null) return true;

        IPlayer player = (byEntity as EntityPlayer)?.Player;
        if (player == null) return true;

        ToolMode currentMode = __instance.GetToolModeObj(api, player);
        if (currentMode == null) return true;

        EnumHandling tmHandling = EnumHandling.PassThrough;
        EnumHandHandling tmHandHandling = EnumHandHandling.NotHandled;
        currentMode.OnHeldAttackStart(slot, byEntity, blockSel, entitySel, ref tmHandHandling, ref tmHandling);

        if (tmHandling == EnumHandling.PassThrough) return true;
        handling = tmHandHandling;

        if (tmHandling == EnumHandling.PreventDefault)
        {
            if (__instance.HeldSounds?.Attack != null && api.World.Side == EnumAppSide.Client)
            {
                api.World.PlaySoundAt(__instance.HeldSounds.Attack, 0, 0, 0, null, 0.9f + (float)api.World.Rand.NextDouble());
            }
        }
        return false;
    }
}

[HarmonyPatch(typeof(CollectibleObject), nameof(CollectibleObject.OnHeldAttackCancel))]
class CollectibleObjectPatch_OnHeldAttackCancel
{
    static bool Prefix(CollectibleObject __instance, ref bool __result, float secondsPassed, ItemSlot slot, EntityAgent byEntity, BlockSelection blockSelection, EntitySelection entitySel, EnumItemUseCancelReason cancelReason)
    {
        ICoreAPI api = byEntity?.Api;
        if (api == null) return true;

        IPlayer player = (byEntity as EntityPlayer)?.Player;
        if (player == null) return true;

        ToolMode currentMode = __instance.GetToolModeObj(api, player);
        if (currentMode == null) return true;

        EnumHandling tmHandling = EnumHandling.PassThrough;
        bool tmResult = currentMode.OnHeldAttackCancel(secondsPassed, slot, byEntity, blockSelection, entitySel, cancelReason, ref tmHandling);
        if (tmHandling == EnumHandling.PassThrough) return true;

        __result = tmResult;
        return false;
    }
}

[HarmonyPatch(typeof(CollectibleObject), nameof(CollectibleObject.OnHeldAttackStep))]
class CollectibleObjectPatch_OnHeldAttackStep
{
    static bool Prefix(CollectibleObject __instance, ref bool __result, float secondsPassed, ItemSlot slot, EntityAgent byEntity, BlockSelection blockSelection, EntitySelection entitySel)
    {
        ICoreAPI api = byEntity?.Api;
        if (api == null) return true;

        IPlayer player = (byEntity as EntityPlayer)?.Player;
        if (player == null) return true;

        ToolMode currentMode = __instance.GetToolModeObj(api, player);
        if (currentMode == null) return true;

        EnumHandling tmHandling = EnumHandling.PassThrough;
        bool tmResult = currentMode.OnHeldAttackStep(secondsPassed, slot, byEntity, blockSelection, entitySel, ref tmHandling);

        if (tmHandling == EnumHandling.PassThrough) return true;
        __result = tmResult;
        return false;
    }
}

[HarmonyPatch(typeof(CollectibleObject), nameof(CollectibleObject.OnHeldAttackStop))]
class CollectibleObjectPatch_OnHeldAttackStop
{
    static bool Prefix(CollectibleObject __instance, float secondsPassed, ItemSlot slot, EntityAgent byEntity, BlockSelection blockSelection, EntitySelection entitySel)
    {
        ICoreAPI api = byEntity?.Api;
        if (api == null) return true;

        IPlayer player = (byEntity as EntityPlayer)?.Player;
        if (player == null) return true;

        ToolMode currentMode = __instance.GetToolModeObj(api, player);
        if (currentMode == null) return true;

        EnumHandling tmHandling = EnumHandling.PassThrough;
        currentMode.OnHeldAttackStop(secondsPassed, slot, byEntity, blockSelection, entitySel, ref tmHandling);

        if (tmHandling == EnumHandling.PassThrough) return true;
        return false;
    }
}

[HarmonyPatch(typeof(CollectibleObject), nameof(CollectibleObject.OnHeldInteractStart))]
class CollectibleObjectPatch_OnHeldInteractStart
{
    static bool Prefix(CollectibleObject __instance, ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, bool firstEvent, ref EnumHandHandling handling)
    {
        ICoreAPI api = byEntity?.Api;
        if (api == null) return true;

        IPlayer player = (byEntity as EntityPlayer)?.Player;
        if (player == null) return true;

        ToolMode currentMode = __instance.GetToolModeObj(api, player);
        if (currentMode == null) return true;

        EnumHandling tmHandling = EnumHandling.PassThrough;
        EnumHandHandling tmHandHandling = EnumHandHandling.NotHandled;

        currentMode.OnHeldInteractStart(slot, byEntity, blockSel, entitySel, firstEvent, ref tmHandHandling, ref tmHandling);
        if (tmHandling == EnumHandling.PassThrough) return true;

        handling = tmHandHandling;
        return false;
    }
}

[HarmonyPatch(typeof(CollectibleObject), nameof(CollectibleObject.OnHeldInteractCancel))]
class CollectibleObjectPatch_OnHeldInteractCancel
{
    static bool Prefix(CollectibleObject __instance, ref bool __result, float secondsUsed, ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, EnumItemUseCancelReason cancelReason)
    {
        ICoreAPI api = byEntity?.Api;
        if (api == null) return true;

        IPlayer player = (byEntity as EntityPlayer)?.Player;
        if (player == null) return true;

        ToolMode currentMode = __instance.GetToolModeObj(api, player);
        if (currentMode == null) return true;

        EnumHandling tmHandling = EnumHandling.PassThrough;
        bool tmResult = currentMode.OnHeldInteractCancel(secondsUsed, slot, byEntity, blockSel, entitySel, cancelReason, ref tmHandling);

        if (tmHandling == EnumHandling.PassThrough) return true;
        __result = tmResult;
        return false;
    }
}

[HarmonyPatch(typeof(CollectibleObject), nameof(CollectibleObject.OnHeldInteractStep))]
class CollectibleObjectPatch_OnHeldInteractStep
{
    static bool Prefix(CollectibleObject __instance, ref bool __result, float secondsUsed, ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel)
    {
        ICoreAPI api = byEntity?.Api;
        if (api == null) return true;

        IPlayer player = (byEntity as EntityPlayer)?.Player;
        if (player == null) return true;

        ToolMode currentMode = __instance.GetToolModeObj(api, player);
        if (currentMode == null) return true;

        EnumHandling tmHandling = EnumHandling.PassThrough;
        bool tmResult = currentMode.OnHeldInteractStep(secondsUsed, slot, byEntity, blockSel, entitySel, ref tmHandling);

        if (tmHandling == EnumHandling.PassThrough) return true;
        __result = tmResult;
        return false;
    }
}

[HarmonyPatch(typeof(CollectibleObject), nameof(CollectibleObject.OnHeldInteractStop))]
class CollectibleObjectPatch_OnHeldInteractStop
{
    static bool Prefix(CollectibleObject __instance, float secondsUsed, ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel)
    {
        ICoreAPI api = byEntity?.Api;
        if (api == null) return true;

        IPlayer player = (byEntity as EntityPlayer)?.Player;
        if (player == null) return true;

        ToolMode currentMode = __instance.GetToolModeObj(api, player);
        if (currentMode == null) return true;

        EnumHandling tmHandling = EnumHandling.PassThrough;
        currentMode.OnHeldInteractStop(secondsUsed, slot, byEntity, blockSel, entitySel, ref tmHandling);

        if (tmHandling == EnumHandling.PassThrough) return true;
        return false;
    }
}