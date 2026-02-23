namespace Mapper.Patches.Mods;

using HarmonyLib;
using Mapper.Util.Reflection;
using Mapper.WorldMap;
using System.Reflection;
using System.Reflection.Emit;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;

public static class ModPatchUtil {
	public static void OnLoaded(MapLayer instance, ICoreAPI api, UniqueQueue<FastVec2i> chunksToGen, object chunksToGenLock) {
		MapperChunkMapLayer mapperLayer = MapperChunkMapLayer.GetInstance(api);
		lock(mapperLayer.OnChunkChanged)
			mapperLayer.OnChunkChanged[instance] = chunkPosition => {
				lock(chunksToGenLock)
					chunksToGen.Enqueue(chunkPosition);
			};
	}

	public static void OnShutDown(MapLayer instance, ICoreAPI api) {
		MapperChunkMapLayer? mapperLayer = MapperChunkMapLayer.GetInstance(api);
		if(mapperLayer != null)
			lock(mapperLayer.OnChunkChanged)
				mapperLayer.OnChunkChanged.Remove(instance);
	}

	public static bool LoadFromChunkPixels(FastVec2i cord, ref int[] pixels, ICoreAPI api, bool useBoxFilter) {
		int? scaleFactor = MapperChunkMapLayer.GetInstance(api).GetScaleFactor((IClientPlayer?)null, cord);
		if(scaleFactor == null)
			return false;
		if(useBoxFilter && scaleFactor != 1)
			pixels = MapperChunkMapLayer.ApplyBoxFilter((int[])pixels.Clone(), (uint)scaleFactor);
		return true;
	}

	/// <summary>
	/// OnOffThreadTick patch is not strictly necessary since we are patching LoadFromChunkPixels,
	/// but it prevents processing chunks unnecessarily when they wouldn't be shown anyway.
	/// </summary>
	public static CodeMatcher OnOffThreadTickTranspiler(CodeMatcher matcher, string className, FieldInfo chunksToGenField) {
		// Find https://github.com/GinoxXP/gi-map/blob/b4e647d54b2532f4daa065dac7c64504a3e619a7/GiMap/Client/AMapLayer.cs#L220
		//   or https://github.com/carlosganhao/VS-GeologyMap/blob/51ffd1314386f9211026167111d0cdb81c94109d/GeologyMap/src/Client/GeologyMapLayer.cs#L251
		// Remember `cord` store instruction
		CodeInstruction cordStloc = matcher.MatchEndForward([
			new(OpCodes.Ldarg_0),
			new(OpCodes.Ldfld, chunksToGenField),
			new(OpCodes.Callvirt, typeof(UniqueQueue<FastVec2i>).GetCheckedMethod("Dequeue", BindingFlags.Instance, [])),
			CodeMatch.IsStloc(),
		]).ThrowIfInvalid($"Could not find `{className}.OnOffThreadTick()::cord`").Instruction;

		// Find https://github.com/GinoxXP/gi-map/blob/b4e647d54b2532f4daa065dac7c64504a3e619a7/GiMap/Client/AMapLayer.cs#L223
		//   or https://github.com/carlosganhao/VS-GeologyMap/blob/51ffd1314386f9211026167111d0cdb81c94109d/GeologyMap/src/Client/GeologyMapLayer.cs#L254
		// Append `|| !ModPatchUtil.HasChunk(this.api, cord)` to the condition
		CodeInstruction skipChunk = matcher.MatchEndForward([
			new(OpCodes.Callvirt, typeof(IBlockAccessor).GetCheckedMethod("IsValidPos", BindingFlags.Instance, [typeof(int), typeof(int), typeof(int)])),
			new(OpCodes.Brfalse),
		]).ThrowIfInvalid($"Could not find `{className}.OnOffThreadTick()::IsValidPos()` to patch").Instruction;
		matcher.Advance(1).InsertAndAdvance([
			new(OpCodes.Ldarg_0),
			new(OpCodes.Ldfld, typeof(MapLayer).GetCheckedField("api", BindingFlags.Instance)),
			CodeInstruction.LoadLocal(cordStloc.LocalIndex()),
			new(OpCodes.Call, typeof(ModPatchUtil).GetCheckedMethod("HasChunk", BindingFlags.Static, [typeof(ICoreAPI), typeof(FastVec2i)])),
			skipChunk.Clone(),
		]);

		return matcher;
	}

	public static bool HasChunk(ICoreAPI api, FastVec2i chunkPosition) {
		return MapperChunkMapLayer.GetInstance(api).GetScaleFactor((IClientPlayer?)null, chunkPosition) != null;
	}
}
