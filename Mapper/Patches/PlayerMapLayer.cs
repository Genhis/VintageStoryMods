namespace Mapper.Patches;

using HarmonyLib;
using Mapper.Util;
using Mapper.Util.Harmony;
using Mapper.Util.Reflection;
using Mapper.WorldMap;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;

[HarmonyPatch(typeof(PlayerMapLayer))]
internal static class PlayerMapLayerPatch {
	[HarmonyPatch("Render")]
	[HarmonyTranspiler]
	internal static IEnumerable<CodeInstruction> Render(IEnumerable<CodeInstruction> instructions) {
		CodeMatcher matcher = new(instructions);

		// Find https://github.com/anegostudios/vsessentialsmod/blob/0dcba6c6ec636e20dbcab6d76c8514fbed282fb3/Systems/WorldMap/EntityLayer/PlayerMapLayer.cs#L85
		// Remember label which increments the enumerator and checks loop condition
		object loopContinue = matcher.MatchEndForward([
			new(OpCodes.Ldarg_0),
			new(OpCodes.Ldfld, typeof(PlayerMapLayer).GetCheckedField("playerTracking", BindingFlags.Instance)),
			new(OpCodes.Callvirt, typeof(SystemRemotePlayerTracking).GetCheckedMethod("GetAllTrackedPlayerPositions", BindingFlags.Instance, [])),
			new(OpCodes.Callvirt),
			CodeMatch.IsStloc(),
			new(OpCodes.Br),
		]).ThrowIfInvalid("Could not find `PlayerMapLayer.Render::GetAllTrackedPlayerPositions().GetEnumerator()`").Instruction.operand;

		return PlayerMapLayerPatch.SharedTranspiler(matcher, "Render", 1, loopContinue).InstructionEnumeration();
	}

	[HarmonyPatch("OnMouseMoveClient")]
	[HarmonyTranspiler]
	internal static IEnumerable<CodeInstruction> OnMouseMoveClient(IEnumerable<CodeInstruction> instructions) {
		CodeMatcher matcher = new(instructions);

		// Find https://github.com/anegostudios/vsessentialsmod/blob/0dcba6c6ec636e20dbcab6d76c8514fbed282fb3/Systems/WorldMap/EntityLayer/PlayerMapLayer.cs#L142
		// Remember loop variable index (it's converted to a while loop)
		int loopVariableIndex = matcher.MatchStartForward([
			new(OpCodes.Callvirt, typeof(IWorldAccessor).GetCheckedProperty("AllOnlinePlayers", BindingFlags.Instance).CheckedGetMethod()),
			CodeMatch.IsStloc(),
			new(OpCodes.Ldc_I4_0),
			CodeMatch.IsStloc(),
			new(OpCodes.Br),
		]).ThrowIfInvalid("Could not find `PlayerMapLayer.OnMouseMoveClient::AllOnlinePlayers`").InstructionAt(3).LocalIndex();

		// Find https://github.com/anegostudios/vsessentialsmod/blob/0dcba6c6ec636e20dbcab6d76c8514fbed282fb3/Systems/WorldMap/EntityLayer/PlayerMapLayer.cs#L168
		// Remember loop continue label (the code increments loop variable, checks loop condition and jumps to the start if able)
		Label loopContinue = matcher.MatchStartForward([
			HarmonyUtil.IsLdloc(loopVariableIndex),
			new(OpCodes.Ldc_I4_1),
			new(OpCodes.Add),
			HarmonyUtil.IsStloc(loopVariableIndex),
		]).ThrowIfInvalid("Could not find `PlayerMapLayer.OnMouseMoveClient::AllOnlinePlayers` loop continue label").Instruction.labels[0];

		return PlayerMapLayerPatch.SharedTranspiler(matcher.Start(), "OnMouseMoveClient", 2, loopContinue).InstructionEnumeration();
	}

	private static CodeMatcher SharedTranspiler(CodeMatcher matcher, string functionName, int mapArgumentIndex, object loopContinue) {
		// Find https://github.com/anegostudios/vsessentialsmod/blob/0dcba6c6ec636e20dbcab6d76c8514fbed282fb3/Systems/WorldMap/EntityLayer/PlayerMapLayer.cs#L91 or
		// Find https://github.com/anegostudios/vsessentialsmod/blob/0dcba6c6ec636e20dbcab6d76c8514fbed282fb3/Systems/WorldMap/EntityLayer/PlayerMapLayer.cs#L145
		// Remember player load instruction
		CodeInstruction ldlocPlayer = matcher.MatchStartForward([
			CodeMatch.IsLdloc(),
			new(OpCodes.Callvirt, typeof(IPlayer).GetCheckedProperty("Entity", BindingFlags.Instance).CheckedGetMethod()),
			new(OpCodes.Brtrue_S),
		]).ThrowIfInvalid($"Could not find `PlayerMapLayer.{functionName}::player`").Instruction;

		// Find https://github.com/anegostudios/vsessentialsmod/blob/0dcba6c6ec636e20dbcab6d76c8514fbed282fb3/Systems/WorldMap/EntityLayer/PlayerMapLayer.cs#L112 or
		// Find https://github.com/anegostudios/vsessentialsmod/blob/0dcba6c6ec636e20dbcab6d76c8514fbed282fb3/Systems/WorldMap/EntityLayer/PlayerMapLayer.cs#L157
		// Prepend `if(!PlayerMapLayerPatch.ClampPosition(this.api, player, worldPos)) continue;`
		return matcher.MatchStartForward([
			CodeMatch.IsLdarg(mapArgumentIndex),
			CodeMatch.IsLdloc(),
			CodeMatch.IsLdloc(),
			new(OpCodes.Callvirt, typeof(GuiElementMap).GetCheckedMethod("TranslateWorldPosToViewPos", BindingFlags.Instance, [typeof(Vec3d), typeof(Vec2f).MakeByRefType()])),
		]).ThrowIfInvalid($"Could not find `PlayerMapLayer.{functionName}::TranslateWorldPosToViewPos()`").InsertAndAdvance([
			new(OpCodes.Ldarg_0),
			new(OpCodes.Ldfld, typeof(MapLayer).GetCheckedField("api", BindingFlags.Instance)),
			ldlocPlayer.Clone(),
			matcher.InstructionAt(1).Clone(),
			new(OpCodes.Call, typeof(PlayerMapLayerPatch).GetCheckedMethod("ClampPosition", BindingFlags.Static, null)),
			new(OpCodes.Brfalse, loopContinue),
		]);
	}

	internal static bool ClampPosition(ICoreAPI api, IPlayer player, Vec3d worldPos) {
		int? scaleFactor = MapperChunkMapLayer.GetInstance(api).GetScaleFactor(player as IClientPlayer, worldPos.ToChunkPosition());
		if(scaleFactor == null)
			return false;

		MapperChunkMapLayer.ClampPosition(worldPos, scaleFactor.Value);
		return true;
	}
}
