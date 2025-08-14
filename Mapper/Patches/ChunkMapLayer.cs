namespace Mapper.Patches;

using HarmonyLib;
using Mapper.Util;
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
		// Append `& this is not MapperChunkMapLayer` to the condition
		matcher.MatchEndForward([
			new(OpCodes.Callvirt, typeof(ICoreAPI).GetCheckedProperty("Side", BindingFlags.Instance).CheckedGetMethod()),
			new(OpCodes.Ldc_I4_2),
			new(OpCodes.Bne_Un),
		]).ThrowIfInvalid("Could not find `if(api.Side == EnumAppSide.Client)` in `ChunkMapLayer..ctor()` to patch").InsertAndAdvance([
			new(OpCodes.Ceq),
			new(OpCodes.Ldarg_0),
			new(OpCodes.Isinst, typeof(MapperChunkMapLayer)),
			new(OpCodes.Ldnull),
			new(OpCodes.Ceq),
			new(OpCodes.And),
		]).SetOpcodeAndAdvance(OpCodes.Brfalse);

		return matcher.InstructionEnumeration();
	}
}
