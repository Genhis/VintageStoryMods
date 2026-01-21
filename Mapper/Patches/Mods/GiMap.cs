namespace Mapper.Patches.Mods.GiMap;

using HarmonyLib;
using Mapper.Util.Harmony;
using Mapper.Util.Reflection;
using Mapper.WorldMap;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using Vintagestory.API.Client;
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
		MapperChunkMapLayer mapperLayer = MapperChunkMapLayer.GetInstance(___api);
		lock(mapperLayer.OnChunkChanged)
			mapperLayer.OnChunkChanged[__instance] = chunkPosition => {
				lock(____chunksToGenLock)
					____chunksToGen.Enqueue(chunkPosition);
			};
	}

	[DynamicHarmonyPatch("OnShutDown")]
	[HarmonyPostfix]
	internal static void OnShutDown(MapLayer __instance, ICoreAPI ___api) {
		MapperChunkMapLayer? mapperLayer = MapperChunkMapLayer.GetInstance(___api);
		if(mapperLayer != null)
			lock(mapperLayer.OnChunkChanged)
				mapperLayer.OnChunkChanged.Remove(__instance);
	}

	// This patch must go before OnOffThreadTick, otherwise it is ignored in some cases (on the first game launch).
	[DynamicHarmonyPatch("LoadFromChunkPixels")]
	[HarmonyPrefix]
	internal static bool LoadFromChunkPixels(MapLayer __instance, FastVec2i cord, ref int[] pixels, ICoreAPI ___api) {
		int? scaleFactor = MapperChunkMapLayer.GetInstance(___api).GetScaleFactor((IClientPlayer?)null, cord);
		if(scaleFactor == null)
			return false;
		if(scaleFactor != 1 && __instance.LayerGroupCode != "chunk-grid")
			pixels = MapperChunkMapLayer.ApplyBoxFilter((int[])pixels.Clone(), (uint)scaleFactor);
		return true;
	}

	// OnOffThreadTick patch is not strictly necessary since we are patching LoadFromChunkPixels,
	// but it prevents processing chunks unnecessarily when they wouldn't be shown anyway,
	// which is especially helpful for the very slow OreMapLayer.
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

		// Find https://github.com/GinoxXP/gi-map/blob/b4e647d54b2532f4daa065dac7c64504a3e619a7/GiMap/Client/AMapLayer.cs#L220
		// Remember `cord` store instruction
		CodeInstruction cordStloc = matcher.MatchEndForward([
			new(OpCodes.Ldarg_0),
			new(OpCodes.Ldfld, chunksToGenField),
			new(OpCodes.Callvirt, typeof(UniqueQueue<FastVec2i>).GetCheckedMethod("Dequeue", BindingFlags.Instance, [])),
			CodeMatch.IsStloc(),
		]).ThrowIfInvalid("Could not find `AMapLayer.OnOffThreadTick()::cord`").Instruction;

		// Find https://github.com/GinoxXP/gi-map/blob/b4e647d54b2532f4daa065dac7c64504a3e619a7/GiMap/Client/AMapLayer.cs#L223
		// Append `|| !Mapper.Patches.Mods.GiMap.AMapLayer.HasChunk(this.api, cord)` to the condition
		CodeInstruction skipChunk = matcher.MatchEndForward([
			new(OpCodes.Callvirt, typeof(IBlockAccessor).GetCheckedMethod("IsValidPos", BindingFlags.Instance, [typeof(int), typeof(int), typeof(int)])),
			new(OpCodes.Brfalse),
		]).ThrowIfInvalid("Could not find `AMapLayer.OnOffThreadTick()::IsValidPos()` to patch").Instruction;
		matcher.Advance(1).InsertAndAdvance([
			new(OpCodes.Ldarg_0),
			new(OpCodes.Ldfld, typeof(MapLayer).GetCheckedField("api", BindingFlags.Instance)),
			CodeInstruction.LoadLocal(cordStloc.LocalIndex()),
			new(OpCodes.Call, typeof(AMapLayer).GetCheckedMethod("HasChunk", BindingFlags.Static, [typeof(ICoreAPI), typeof(FastVec2i)])),
			skipChunk.Clone(),
		]);

		return matcher.InstructionEnumeration();
	}

	internal static bool HasChunk(ICoreAPI api, FastVec2i chunkPosition) {
		return MapperChunkMapLayer.GetInstance(api).GetScaleFactor((IClientPlayer?)null, chunkPosition) != null;
	}
}
