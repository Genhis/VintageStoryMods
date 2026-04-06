namespace Mapper.Patches;

using HarmonyLib;
using Mapper.Util;
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
		return PlayerMapLayerPatch.SharedTranspiler(new CodeMatcher(instructions), "Render", 1).InstructionEnumeration();
	}

	[HarmonyPatch("OnMouseMoveClient")]
	[HarmonyTranspiler]
	internal static IEnumerable<CodeInstruction> OnMouseMoveClient(IEnumerable<CodeInstruction> instructions) {
		return PlayerMapLayerPatch.SharedTranspiler(new CodeMatcher(instructions), "OnMouseMoveClient", 2).InstructionEnumeration();
	}

	private static CodeMatcher SharedTranspiler(CodeMatcher matcher, string functionName, int mapArgumentIndex) {
		// Find https://github.com/anegostudios/vsessentialsmod/blob/2115302daf2bde86bf751ba64fe9a955735955d5/Systems/WorldMap/EntityLayer/PlayerMapLayer.cs#L82 or
		// Find https://github.com/anegostudios/vsessentialsmod/blob/2115302daf2bde86bf751ba64fe9a955735955d5/Systems/WorldMap/EntityLayer/PlayerMapLayer.cs#L137
		// Remember label which increments the enumerator and checks loop condition
		object loopContinue = matcher.MatchEndForward([
			new(OpCodes.Ldarg_0),
			new(OpCodes.Ldfld, typeof(PlayerMapLayer).GetCheckedField("playerTracking", BindingFlags.Instance)),
			new(OpCodes.Callvirt, typeof(SystemRemotePlayerTracking).GetCheckedMethod("GetAllTrackedPlayerPositions", BindingFlags.Instance, [])),
			new(OpCodes.Callvirt),
			CodeMatch.IsStloc(),
			new(OpCodes.Br),
		]).ThrowIfInvalid($"Could not find `PlayerMapLayer.{functionName}::GetAllTrackedPlayerPositions().GetEnumerator()`").Instruction.operand;

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
