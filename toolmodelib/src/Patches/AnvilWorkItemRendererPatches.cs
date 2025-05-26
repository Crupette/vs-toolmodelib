using System.Collections.Generic;
using System.Reflection.Emit;
using HarmonyLib;
using ToolModeLib.Content;
using Vintagestory.GameContent;

namespace ToolModeLib.Patches;

[HarmonyPatch(typeof(AnvilWorkItemRenderer), nameof(AnvilWorkItemRenderer.OnRenderFrame))]
class AnvilWorkItemRendererPatch_OnRenderFrame
{
    static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        var codeMatcher = new CodeMatcher(instructions);
        return codeMatcher.MatchStartForward(
            new CodeMatch(OpCodes.Isinst, typeof(ItemHammer))
        ).ThrowIfInvalid("Failed to patch AnvilWorkItemRenderer.OnRenderFrame")
        .SetOperandAndAdvance(typeof(ItemHammerWithModes))
         .Instructions();
    }
}