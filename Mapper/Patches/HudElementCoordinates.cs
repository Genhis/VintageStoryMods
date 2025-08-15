namespace Mapper.Patches;

using HarmonyLib;
using Mapper.Util;
using Mapper.Util.Reflection;
using Mapper.WorldMap;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.Client.NoObf;

[HarmonyPatch(typeof(HudElementCoordinates))]
internal static class HudElementCoordinatesPatch {
	[HarmonyPatch("OnBlockTexturesLoaded")]
	[HarmonyTranspiler]
	internal static IEnumerable<CodeInstruction> OnBlockTexturesLoaded(IEnumerable<CodeInstruction> instructions) {
		// Set refresh rate to 50ms instead of 250ms
		return new CodeMatcher(instructions).MatchStartForward([
			new(OpCodes.Ldc_I4, 250),
			new(OpCodes.Ldc_I4_0),
			new(OpCodes.Callvirt, typeof(IEventAPI).GetCheckedMethod("RegisterGameTickListener", BindingFlags.Instance, [typeof(Action<float>), typeof(int), typeof(int)])),
		]).ThrowIfInvalid("Could not find `HudElementCoordinates.OnBlockTexturesLoaded()::RegisterGameTickListener()` to patch").SetOperandAndAdvance(50).InstructionEnumeration();
	}

	[HarmonyPatch("Every250ms")]
	[HarmonyTranspiler]
	internal static IEnumerable<CodeInstruction> Every250ms(IEnumerable<CodeInstruction> instructions, ILGenerator generator) {
		CodeMatcher matcher = new(instructions, generator);

		// Find SetNewText() function and extract first argument to store unknown position string in it
		CodeInstruction ldlocText = matcher.MatchStartForward([
			new(OpCodes.Ldarg_0),
			new(OpCodes.Call, typeof(GuiDialog).GetCheckedProperty("SingleComposer", BindingFlags.Instance).CheckedGetMethod()),
			new(OpCodes.Ldstr, "text"),
			new(OpCodes.Call, typeof(GuiElementDynamicTextHelper).GetCheckedMethod("GetDynamicText", BindingFlags.Static, [typeof(GuiComposer), typeof(string)])),
			CodeMatch.IsLdloc(),
			new(),
			new(),
			new(),
			new(OpCodes.Callvirt, typeof(GuiElementDynamicText).GetCheckedMethod("SetNewText", BindingFlags.Instance, [typeof(string), typeof(bool), typeof(bool), typeof(bool)])),
		]).ThrowIfInvalid("Could not find `HudElementCoordinates.Every250ms()::SetNewText()`").CreateLabel(out Label skipTextGeneration).InstructionAt(4);

		// Find the part which checks if the coordinates HUD is open
		matcher.Start().MatchEndForward([
			new(OpCodes.Ldarg_0),
			new(OpCodes.Callvirt, typeof(GuiDialog).GetCheckedMethod("IsOpened", BindingFlags.Instance, Type.EmptyTypes)),
			new(OpCodes.Brtrue_S),
			new(OpCodes.Ret),
		]).ThrowIfInvalid("Could not find `if(!this.IsOpened()) return;` in `HudElementCoordinates.Every250ms()`").Advance(1);

		// Append:
		// int? scaleFactor = HudElementCoordinatesPatch.GetScaleFactor(this.capi);
		// if(scaleFactor == null) {
		//   text = Lang.Get("mapper:hud-unknown-position");
		//   goto skipTextGeneration;
		// }
		CodeInstruction newStart = new(OpCodes.Ldarg_0);
		matcher.TransferLabels(newStart).CreateLabel(out Label originalStart).DeclareLocal(typeof(int?), out LocalBuilder scaleFactor).InsertAndAdvance([
			newStart,
			CodeInstruction.LoadField(typeof(GuiDialog), "capi"),
			CodeInstruction.Call(typeof(HudElementCoordinatesPatch), "GetScaleFactor", [typeof(ICoreClientAPI)]),
			CodeInstruction.StoreLocal(scaleFactor.LocalIndex),
			CodeInstruction.LoadLocal(scaleFactor.LocalIndex, true),
			new(OpCodes.Call, typeof(int?).GetCheckedProperty("HasValue", BindingFlags.Instance).CheckedGetMethod()),
			new(OpCodes.Brtrue_S, originalStart),
			new(OpCodes.Ldstr, "mapper:hud-unknown-position"),
			CodeInstruction.Call(typeof(Array), "Empty", Type.EmptyTypes, [typeof(object)]),
			CodeInstruction.Call(typeof(Lang), "Get", [typeof(string), typeof(object[])]),
			CodeInstruction.StoreLocal(ldlocText.LocalIndex()),
			new(OpCodes.Br, skipTextGeneration),
		]);

		return matcher.InstructionEnumeration();
	}

	internal static int? GetScaleFactor(ICoreClientAPI api) {
		IClientPlayer player = api.World.Player;
		if(player.WorldData.CurrentGameMode != EnumGameMode.Survival)
			return 1;
		return MapperChunkMapLayer.GetInstance(api).GetScaleFactor(player.Entity.Pos.ToChunkPosition());
	}
}
