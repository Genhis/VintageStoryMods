namespace Mapper.Patches;

using HarmonyLib;
using Mapper.Util;
using Mapper.Util.Reflection;
using Mapper.WorldMap;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.GameContent;

[HarmonyPatch(typeof(WaypointMapLayer))]
internal static class WaypointMapLayerPatch {
	[HarmonyPatch("AddWp")]
	[HarmonyPrefix]
	internal static bool AddWp(IServerPlayer player, Vec3d pos, ref TextCommandResult __result, ICoreAPI ___api) {
		int? scaleFactor = MapperChunkMapLayer.GetInstance(___api).GetScaleFactor(player, pos.ToChunkPosition());
		if(scaleFactor == null) {
			__result = TextCommandResult.Success(Lang.GetL(player.LanguageCode, "mapper:error-unexplored-map"));
			return false;
		}

		MapperChunkMapLayer.ClampPosition(pos, scaleFactor.Value);
		return true;
	}

	[HarmonyPatch("Event_PlayerDeath")]
	[HarmonyTranspiler]
	internal static IEnumerable<CodeInstruction> EventPlayerDeath(IEnumerable<CodeInstruction> instructions, ILGenerator generator) {
		CodeMatcher matcher = new(instructions, generator);

		// Find https://github.com/anegostudios/vsessentialsmod/blob/b447263a4860f52d92fd29f800f3f1fd8e905c6a/Systems/WorldMap/WaypointLayer/WaypointMapLayer.cs#L296
		// Remember the instruction which skips waypoint creation
		CodeInstruction skipWaypoint = matcher.MatchEndForward([
			new(OpCodes.Callvirt, typeof(ITreeAttribute).GetCheckedMethod("GetBool", BindingFlags.Instance, [typeof(string), typeof(bool)])),
			new(OpCodes.Brfalse_S),
		]).ThrowIfInvalid("Could not find `WaypointMapLayer.Event_PlayerDeath()::GetBool()`").Instruction;

		// Insert `int? scaleFactor = WaypointMapLayerPatch.GetScaleFactor(byPlayer);` at the start
		// Insert `scaleFactor == null ||` as the first condition
		matcher.Start().DeclareLocal(typeof(int?), out LocalBuilder scaleFactor).InsertAndAdvance([
			new(OpCodes.Ldarg_1),
			CodeInstruction.Call(typeof(WaypointMapLayerPatch), "GetScaleFactor", [typeof(IServerPlayer)]),
			CodeInstruction.StoreLocal(scaleFactor.LocalIndex),
			CodeInstruction.LoadLocal(scaleFactor.LocalIndex, true),
			new(OpCodes.Call, typeof(int?).GetCheckedProperty("HasValue", BindingFlags.Instance).CheckedGetMethod()),
			skipWaypoint.Clone(),
		]);

		// Find https://github.com/anegostudios/vsessentialsmod/blob/b447263a4860f52d92fd29f800f3f1fd8e905c6a/Systems/WorldMap/WaypointLayer/WaypointMapLayer.cs#L313
		// Change line to `Position = MapperChunkMapLayer.ClampPosition(byPlayer.Entity.Pos.XYZ, scaleFactor.Value),`
		matcher.MatchEndForward([
			new(OpCodes.Callvirt, typeof(EntityPos).GetCheckedProperty("XYZ", BindingFlags.Instance).CheckedGetMethod()),
			new(OpCodes.Stfld, typeof(Waypoint).GetCheckedField("Position", BindingFlags.Instance)),
		]).ThrowIfInvalid("Could not find `WaypointMapLayer.Event_PlayerDeath()::Position` to patch").InsertAndAdvance([
			CodeInstruction.LoadLocal(scaleFactor.LocalIndex, true),
			new(OpCodes.Call, typeof(int?).GetCheckedProperty("Value", BindingFlags.Instance).CheckedGetMethod()),
			CodeInstruction.Call(typeof(MapperChunkMapLayer), "ClampPosition", [typeof(Vec3d), typeof(int)]),
		]);

		return matcher.InstructionEnumeration();
	}

	internal static int? GetScaleFactor(IServerPlayer player) {
		return MapperChunkMapLayer.GetInstance(player.Entity.Api).GetScaleFactor(player, player.Entity.Pos.ToChunkPosition());
	}
}
