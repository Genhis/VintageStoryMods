namespace Mapper.Patches.Mods;

using HarmonyLib;
using Mapper.Util.Harmony;
using Mapper.Util.Reflection;
using System.Collections.Generic;
using System.Reflection;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;

[DynamicHarmonyPatch(AssemblyName = "GeologyMap", TypeName = "Vintagestory.GameContent.GeologyMapLayer")]
internal static class GeologyMapLayer {
#pragma warning disable CS0649, CS8618
	private static System.Type type;
#pragma warning restore CS0649, CS8618

	[DynamicHarmonyPatch("OnLoaded")]
	[HarmonyPostfix]
	internal static void OnLoaded(MapLayer __instance, ICoreAPI ___api, UniqueQueue<FastVec2i> ___chunksToGen, object ___chunksToGenLock) {
		ModPatchUtil.OnLoaded(__instance, ___api, ___chunksToGen, ___chunksToGenLock);
	}

	[DynamicHarmonyPatch("OnShutDown")]
	[HarmonyPostfix]
	internal static void OnShutDown(MapLayer __instance, ICoreAPI ___api) {
		ModPatchUtil.OnShutDown(__instance, ___api);
	}

	// This patch must go before OnOffThreadTick, otherwise it is ignored in some cases (on the first game launch).
	[DynamicHarmonyPatch("loadFromChunkPixels")]
	[HarmonyPrefix]
	internal static bool LoadFromChunkPixels(FastVec2i cord, ref int[] pixels, ICoreAPI ___api) {
		return ModPatchUtil.LoadFromChunkPixels(cord, ref pixels, ___api, true);
	}

	[DynamicHarmonyPatch("OnOffThreadTick", Optional = true)]
	[HarmonyTranspiler]
	internal static IEnumerable<CodeInstruction> OnOffThreadTick(IEnumerable<CodeInstruction> instructions) {
		FieldInfo chunksToGenField = GeologyMapLayer.type.GetCheckedField("chunksToGen", BindingFlags.Instance);
		return ModPatchUtil.OnOffThreadTickTranspiler(new CodeMatcher(instructions), "GeologyMapLayer", chunksToGenField).InstructionEnumeration();
	}
}
