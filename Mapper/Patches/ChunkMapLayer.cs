namespace Mapper.Patches;

using HarmonyLib;
using Mapper.Util.Harmony;
using Mapper.Util.Reflection;
using Mapper.WorldMap;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using Vintagestory.API.Common;
using Vintagestory.GameContent;

[HarmonyPatch(typeof(ChunkMapLayer))]
internal static class ChunkMapLayerPatch {
	[HarmonyPatch(MethodType.Constructor, [typeof(ICoreAPI), typeof(IWorldMapManager)])]
	[HarmonyTranspiler]
	internal static IEnumerable<CodeInstruction> Constructor(IEnumerable<CodeInstruction> instructions, ILGenerator generator) {
		CodeMatcher matcher = new(instructions, generator);

		// Find https://github.com/anegostudios/vsessentialsmod/blob/b447263a4860f52d92fd29f800f3f1fd8e905c6a/Systems/WorldMap/ChunkLayer/ChunkMapLayer.cs#L106
		// Surround it with `if(this is not MapperChunkMapLayer)`
		matcher.MatchStartForward([
			new CodeMatch(OpCodes.Callvirt, typeof(IEventAPI).GetCheckedEvent("ChunkDirty", BindingFlags.Instance).CheckedAddMethod()),
		]).ThrowIfInvalid("Could not find `ChunkMapLayer..ctor()::add_ChunkDirty` to patch").Advance(1).CreateLabel(out Label skipChunkDirty).MatchStartBackwards([
			new(OpCodes.Ldarg_1),
			new(OpCodes.Callvirt, typeof(ICoreAPI).GetCheckedProperty("Event", BindingFlags.Instance).CheckedGetMethod()),
		]).ThrowIfInvalid("Could not find `ChunkMapLayer..ctor()::api.Event` to patch").InsertAndAdvanceTransferLabels([
			new(OpCodes.Ldarg_0),
			new(OpCodes.Isinst, typeof(MapperChunkMapLayer)),
			new(OpCodes.Brtrue_S, skipChunkDirty),
		]);

		// Find https://github.com/anegostudios/vsessentialsmod/blob/b447263a4860f52d92fd29f800f3f1fd8e905c6a/Systems/WorldMap/ChunkLayer/ChunkMapLayer.cs#L114
		// Remember skip label
		object skipClientMapLoading = matcher.MatchEndForward([
			new(OpCodes.Callvirt, typeof(ICoreAPI).GetCheckedProperty("Side", BindingFlags.Instance).CheckedGetMethod()),
			new(OpCodes.Ldc_I4_2),
			new(OpCodes.Bne_Un),
		]).ThrowIfInvalid("Could not find `if(api.Side == EnumAppSide.Client)` in `ChunkMapLayer..ctor()`").Instruction.operand;

		// Find https://github.com/anegostudios/vsessentialsmod/blob/0dcba6c6ec636e20dbcab6d76c8514fbed282fb3/Systems/WorldMap/ChunkLayer/ChunkMapLayer.cs#L121
		// Prepend `if(this is MapperChunkMapLayer) goto skipClientMapLoading;`
		matcher.MatchStartForward([
			new(OpCodes.Ldarg_1),
			new(OpCodes.Callvirt, typeof(ICoreAPI).GetCheckedProperty("World", BindingFlags.Instance).CheckedGetMethod()),
			new(OpCodes.Callvirt, typeof(IWorldAccessor).GetCheckedProperty("Logger", BindingFlags.Instance).CheckedGetMethod()),
			new(OpCodes.Ldstr),
			new(OpCodes.Callvirt, typeof(ILogger).GetCheckedMethod("Notification", BindingFlags.Instance, [typeof(string)])),
		]).ThrowIfInvalid("Could not find 'Loading world map cache db' log entry in `ChunkMapLayer..ctor()` to patch").InsertAndAdvance([
			new(OpCodes.Ldarg_0),
			new(OpCodes.Isinst, typeof(MapperChunkMapLayer)),
			new(OpCodes.Brtrue, skipClientMapLoading),
		]);

		return matcher.InstructionEnumeration();
	}
}
