namespace Mapper.Patches.Mods.GiMap;

using HarmonyLib;
using Mapper.Util.Harmony;
using Mapper.Util.Reflection;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;

[DynamicHarmonyPatch(AssemblyName = "GiMap", TypeName = "GiMap.Client.AMapLayer")]
internal static class AMapLayer {
#pragma warning disable CS0649, CS8618
	private static Type type;
#pragma warning restore CS0649, CS8618

	[DynamicHarmonyPatch("OnLoaded")]
	[HarmonyPostfix]
	internal static void OnLoaded(MapLayer __instance, ICoreAPI ___api, UniqueQueue<FastVec2i> ____chunksToGen, object ____chunksToGenLock) {
		ModPatchUtil.OnLoaded(__instance, ___api, ____chunksToGen, ____chunksToGenLock);
	}

	[DynamicHarmonyPatch("OnShutDown")]
	[HarmonyPostfix]
	internal static void OnShutDown(MapLayer __instance, ICoreAPI ___api) {
		ModPatchUtil.OnShutDown(__instance, ___api);
	}

	// This patch must go before OnOffThreadTick, otherwise it is ignored in some cases (on the first game launch).
	[DynamicHarmonyPatch("LoadFromChunkPixels")]
	[HarmonyPrefix]
	internal static bool LoadFromChunkPixels(MapLayer __instance, FastVec2i cord, ref int[] pixels, ICoreAPI ___api) {
		return ModPatchUtil.LoadFromChunkPixels(cord, ref pixels, ___api, __instance.LayerGroupCode != "chunk-grid");
	}

	[DynamicHarmonyPatch("OnOffThreadTick", Optional = true)]
	[HarmonyTranspiler]
	internal static IEnumerable<CodeInstruction> OnOffThreadTick(IEnumerable<CodeInstruction> instructions, ILGenerator generator) {
		FieldInfo chunksToGenField = AMapLayer.type.GetCheckedField("_chunksToGen", BindingFlags.Instance);
		CodeMatcher matcher = new(instructions, generator);

		// Find https://github.com/GinoxXP/gi-map/blob/b4e647d54b2532f4daa065dac7c64504a3e619a7/GiMap/Client/AMapLayer.cs#L209
		// Replace line with `int quantityToGen = this is OreMapLayer ? Math.Min(_chunksToGen.Count, 1) : _chunksToGen.Count`
		// Because OreMapLayer is very resource-hungry and restricts other threads
		Type? oreMapLayerType = AMapLayer.type.Assembly.GetType("GiMap.Client.OreMapLayer");
		if(oreMapLayerType != null)
			matcher.MatchEndForward([
				new(OpCodes.Ldarg_0),
				new(OpCodes.Ldfld, chunksToGenField),
				new(OpCodes.Callvirt, typeof(UniqueQueue<FastVec2i>).GetCheckedProperty("Count", BindingFlags.Instance).CheckedGetMethod()),
			]).ThrowIfInvalid("Could not find `AMapLayer.OnOffThreadTick()::_chunksToGen.Count` to patch").Advance(1).CreateLabel(out Label skipCountRestriction).InsertAndAdvance([
				new(OpCodes.Ldarg_0),
				new(OpCodes.Isinst, oreMapLayerType),
				new(OpCodes.Brfalse_S, skipCountRestriction),
				new(OpCodes.Ldc_I4_1),
				new(OpCodes.Call, typeof(Math).GetCheckedMethod("Min", BindingFlags.Static, [typeof(int), typeof(int)])),
			]);

		return ModPatchUtil.OnOffThreadTickTranspiler(matcher, "AMapLayer", chunksToGenField).InstructionEnumeration();
	}
}
