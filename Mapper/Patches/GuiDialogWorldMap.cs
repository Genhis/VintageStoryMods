namespace Mapper.Patches;

using HarmonyLib;
using Mapper.Util.Reflection;
using Mapper.WorldMap;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.GameContent;

[HarmonyPatch(typeof(GuiDialogWorldMap))]
internal static class GuiDialogWorldMapPatch {
	[HarmonyPatch("Open")]
	[HarmonyPrefix]
	internal static bool Open(GuiDialogWorldMap __instance, EnumDialogType type, ICoreClientAPI ___capi) {
		if(type == EnumDialogType.Dialog || !MapperChunkMapLayer.HasLastKnownPosition(___capi))
			return true;
		if(__instance.IsOpened())
			__instance.TryClose();
		return false;
	}

	[HarmonyPatch("TryClose")]
	[HarmonyTranspiler]
	internal static IEnumerable<CodeInstruction> TryClose(IEnumerable<CodeInstruction> instructions) {
		CodeMatcher matcher = new(instructions);

		// Find https://github.com/anegostudios/vsessentialsmod/blob/b447263a4860f52d92fd29f800f3f1fd8e905c6a/Systems/WorldMap/GuiDialogWorldMap.cs#L267
		// Insert `&& MapperChunkMapLayer.HasLastKnownPosition(this.capi)` after Dialog
		CodeInstruction skipOpenHud = matcher.MatchEndForward([
			new(OpCodes.Ldarg_0),
			new(OpCodes.Callvirt, typeof(GuiDialog).GetCheckedProperty("DialogType", BindingFlags.Instance).CheckedGetMethod()),
			new(OpCodes.Brtrue_S),
		]).ThrowIfInvalid("Could not find `GuiDialogWorldMap.TryClose()::DialogType` to patch").Instruction;
		matcher.Advance(1).InsertAndAdvance([
			new(OpCodes.Ldarg_0),
			CodeInstruction.LoadField(typeof(GuiDialog), "capi"),
			CodeInstruction.Call(typeof(MapperChunkMapLayer), "HasLastKnownPosition", [typeof(ICoreAPI)]),
			skipOpenHud.Clone(),
		]);

		return matcher.InstructionEnumeration();
	}
}
