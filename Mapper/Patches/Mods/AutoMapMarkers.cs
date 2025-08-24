namespace Mapper.Patches.Mods;

using HarmonyLib;
using Mapper.Util.Harmony;
using Mapper.WorldMap;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;

[DynamicHarmonyPatch(AssemblyName = "automapmarkers")]
internal static class AutoMapMarkers {
	[DynamicHarmonyPatch("Egocarib.AutoMapMarkers.Utilities.WaypointUtil", "AddWaypoint")]
	[HarmonyPrefix]
	internal static bool AddWaypoint(Vec3d? position, IServerPlayer? ___ServerPlayer) {
		if(position == null || ___ServerPlayer == null)
			return true;

		int? scaleFactor = MapperChunkMapLayer.GetInstance(___ServerPlayer.Entity.Api).GetScaleFactor(___ServerPlayer, new FastVec2i((int)position.X / 32, (int)position.Z / 32));
		if(scaleFactor == null) {
			___ServerPlayer.SendMessage(GlobalConstants.InfoLogChatGroup, Lang.GetL(___ServerPlayer.LanguageCode, "mapper:error-unexplored-map"), EnumChatType.Notification);
			return false;
		}

		MapperChunkMapLayer.ClampPosition(position, scaleFactor.Value);
		return true;
	}
}
