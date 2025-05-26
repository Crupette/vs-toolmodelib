using System.Collections;
using System.Collections.Generic;
using System.Reflection.Emit;
using HarmonyLib;
using ToolModeLib.Content;
using Vintagestory.GameContent;

namespace ToolModeLib.Patches;

[HarmonyPatch(typeof(ClayFormRenderer), "RenderRecipeOutLine")]
class ClayFormRendererPatch_RenderRecipeOutline
{
    static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        var codeMatcher = new CodeMatcher(instructions);
        return codeMatcher.MatchStartForward(
            new CodeMatch(OpCodes.Isinst, typeof(ItemClay))
        ).ThrowIfInvalid("Failed to patch ClayFormRenderer.RenderRecipeOutline")
        .SetOperandAndAdvance(typeof(ItemClayWithModes))
         .Instructions();
    }
}