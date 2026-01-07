namespace Mapper.Patches.Mods;

using HarmonyLib;
using Mapper.Util.Harmony;
using Mapper.Util.Reflection;
using Mapper.WorldMap;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;

[DynamicHarmonyPatch(AssemblyName = "automapmarkers")]
internal static class AutoMapMarkers {
#pragma warning disable CS8618
	private static Assembly assembly;
#pragma warning restore CS8618

	[DynamicHarmonyPatch("Egocarib.AutoMapMarkers.Utilities.WaypointUtil", "AddWaypoint")]
	[HarmonyTranspiler]
	internal static IEnumerable<CodeInstruction> AddWaypoint(IEnumerable<CodeInstruction> instructions, ILGenerator generator) {
		// Find https://github.com/egocarib/Vintage-Story-Mods/blob/a65aa97bf55fee5b66d1550c90f16019288f9763/mods/automapmarkers/src/Utilities/WaypointUtil.cs#L55
		// Append `&& AutoMapMarkers.AddWaypointInsert(this.ServerPlayer, position)` to the condition
		CodeInstruction newStart = new(OpCodes.Ldarg_0);
		return new CodeMatcher(instructions, generator).MatchEndForward([
			new(OpCodes.Ldfld, AutoMapMarkers.assembly.GetCheckedType("Egocarib.AutoMapMarkers.Settings.MapMarkerConfig+Settings+AutoMapMarkerSetting").GetCheckedField("Enabled", BindingFlags.Instance)),
			new(OpCodes.Brtrue_S),
			new(OpCodes.Ret),
		]).ThrowIfInvalid("Could not find `if(!settings.Enabled)` in `automapmarkers::WaypointUtil.AddWaypoint` to patch").Advance(1).TransferLabels(newStart).CreateLabel(out Label skipEarlyReturn).InsertAndAdvance([
			newStart,
			new(OpCodes.Ldfld, AutoMapMarkers.assembly.GetCheckedType("Egocarib.AutoMapMarkers.Utilities.WaypointUtil").GetCheckedField("ServerPlayer", BindingFlags.Instance)),
			new(OpCodes.Ldarg_1),
			new(OpCodes.Call, typeof(AutoMapMarkers).GetCheckedMethod("AddWaypointInsert", BindingFlags.Static, [typeof(IServerPlayer), typeof(Vec3d)])),
			new(OpCodes.Brtrue_S, skipEarlyReturn),
			new(OpCodes.Ret),
		]).InstructionEnumeration();
	}

	internal static bool AddWaypointInsert(IServerPlayer player, Vec3d position) {
		MapperChunkMapLayer layer = MapperChunkMapLayer.GetInstance(player.Entity.Api);
		int? scaleFactor = layer.GetScaleFactor(player, new FastVec2i((int)position.X / 32, (int)position.Z / 32));
		if(scaleFactor == null) {
			layer.TrySendUnrevealedMapMessage(player);
			return false;
		}

		MapperChunkMapLayer.ClampPosition(position, scaleFactor.Value);
		return true;
	}
}
