namespace Mapper.Patches;

using HarmonyLib;
using Mapper.Util;
using Mapper.WorldMap;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Server;
using Vintagestory.GameContent;

[HarmonyPatch(typeof(ModSystemOreMap))]
internal static class ModSystemOreMapPatch {
	[HarmonyPatch("DidProbe")]
	[HarmonyPrefix]
	internal static bool DidProbe(PropickReading results, IServerPlayer splr, ICoreAPI ___api) {
		int? scaleFactor = MapperChunkMapLayer.GetInstance(___api).GetScaleFactor(splr, results.Position.ToChunkPosition());
		if(scaleFactor == null) {
			splr.SendMessage(GlobalConstants.InfoLogChatGroup, Lang.GetL(splr.LanguageCode, "mapper:error-unexplored-map-propick"), EnumChatType.Notification);
			return false;
		}

		MapperChunkMapLayer.ClampPosition(results.Position, scaleFactor.Value);
		return true;
	}
}
