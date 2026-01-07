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
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;

[HarmonyPatch(typeof(GuiElementMap))]
public static class GuiElementMapPatch {
	public static void CenterMapToPlayer(this GuiElementMap map) {
		IClientPlayer player = map.Api.World.Player;
		EntityPos entityPos = player.Entity.Pos;
		int? scaleFactor = MapperChunkMapLayer.GetInstance(map.Api).GetScaleFactor(player, entityPos.ToChunkPosition());
		if(scaleFactor != null)
			map.CenterMapTo(MapperChunkMapLayer.ClampPosition(entityPos.XYZ, scaleFactor.Value).AsBlockPos);
	}

	[HarmonyPatch("OnKeyDown")]
	[HarmonyTranspiler]
	internal static IEnumerable<CodeInstruction> OnKeyDown(IEnumerable<CodeInstruction> instructions) {
		// Find https://github.com/anegostudios/vsessentialsmod/blob/b447263a4860f52d92fd29f800f3f1fd8e905c6a/Systems/WorldMap/GuiElementMap.cs#L322
		// Replace line with `GuiElementMapPatch.CenterMapToPlayer(this)`
		return new CodeMatcher(instructions).MatchStartForward([
			new(OpCodes.Ldarg_0),
			new(OpCodes.Ldarg_1),
			new(OpCodes.Callvirt, typeof(ICoreClientAPI).GetCheckedProperty("World", BindingFlags.Instance).CheckedGetMethod()),
			new(OpCodes.Callvirt, typeof(IClientWorldAccessor).GetCheckedProperty("Player", BindingFlags.Instance).CheckedGetMethod()),
			new(OpCodes.Callvirt, typeof(IPlayer).GetCheckedProperty("Entity", BindingFlags.Instance).CheckedGetMethod()),
			new(OpCodes.Ldfld, typeof(Entity).GetCheckedField("Pos", BindingFlags.Instance)),
			new(OpCodes.Callvirt, typeof(EntityPos).GetCheckedProperty("AsBlockPos", BindingFlags.Instance).CheckedGetMethod()),
			new(OpCodes.Call, typeof(GuiElementMap).GetCheckedMethod("CenterMapTo", BindingFlags.Instance, [typeof(BlockPos)])),
		]).ThrowIfInvalid("Could not find `GuiElementMap.OnKeyDown()::CenterMapTo()` to patch").Advance(1).RemoveInstructions(6).SetOperandAndAdvance(typeof(GuiElementMapPatch).GetCheckedMethod("CenterMapToPlayer", BindingFlags.Static, [typeof(GuiElementMap)])).InstructionEnumeration();
	}

	[HarmonyPatch("ComposeElements")]
	[HarmonyPrefix]
	internal static bool ComposeElements(GuiElementMap __instance, Vec3d ___prevPlayerPos) {
		__instance.Bounds.CalcWorldBounds();
		__instance.chunkViewBoundsBefore = new();

		Vec3d position = MapperChunkMapLayer.GetInstance(__instance.Api).GetPlayerOrLastKnownPosition();
		___prevPlayerPos.Set(position);
		__instance.CenterMapTo(position.AsBlockPos);
		return false;
	}

	[HarmonyPatch("EnsureMapFullyLoaded")]
	[HarmonyTranspiler]
	internal static IEnumerable<CodeInstruction> EnsureMapFullyLoaded(IEnumerable<CodeInstruction> instructions) {
		// Find https://github.com/anegostudios/vsessentialsmod/blob/b447263a4860f52d92fd29f800f3f1fd8e905c6a/Systems/WorldMap/GuiElementMap.cs#L256
		// Append `.GrowBy(1, 0, 1)`
		return new CodeMatcher(instructions).MatchEndForward([
			new(OpCodes.Ldc_I4_S, (sbyte)GlobalConstants.ChunkSize),
			new(OpCodes.Callvirt, typeof(Cuboidi).GetCheckedMethod("Div", BindingFlags.Instance, [typeof(int)])),
			new(OpCodes.Pop),
		]).ThrowIfInvalid("Could not find `GuiElementMap.EnsureMapFullyLoaded()::chunkviewBounds` to patch").InsertAndAdvance([
			new(OpCodes.Ldc_I4_1),
			new(OpCodes.Ldc_I4_0),
			new(OpCodes.Ldc_I4_1),
			CodeInstruction.Call(typeof(Cuboidi), "GrowBy", [typeof(int), typeof(int), typeof(int)]),
		]).InstructionEnumeration();
	}

	[HarmonyPatch("RenderInteractiveElements")]
	[HarmonyPrefix]
	internal static void RenderInteractiveElements(GuiElementMap __instance, Vec3d ___prevPlayerPos, bool ___snapToPlayer) {
		MapperChunkMapLayer layer = MapperChunkMapLayer.GetInstance(__instance.Api);
		IClientPlayer player = __instance.Api.World.Player;
		int? scaleFactor = layer.GetScaleFactor(player, player.Entity.Pos.ToChunkPosition());
		if(scaleFactor == null) {
			if(layer.UpdateLastKnownPosition(___prevPlayerPos)) {
				GuiDialogWorldMap? worldMapDialog = __instance.Api.ModLoader.GetModSystem<WorldMapManager>().worldMapDlg;
				if(worldMapDialog?.DialogType == EnumDialogType.HUD)
					worldMapDialog.TryClose();
			}
			return;
		}

		Vec3d currentPos = MapperChunkMapLayer.ClampPosition(player.Entity.Pos.XYZ, scaleFactor.Value);
		double diffX = currentPos.X - ___prevPlayerPos.X;
		double diffZ = currentPos.Z - ___prevPlayerPos.Z;
		if(Math.Abs(diffX) <= 0.0002 && Math.Abs(diffZ) <= 0.0002)
			return;

		if(___snapToPlayer) {
			double halfWidth = __instance.Bounds.InnerWidth / 2 / __instance.ZoomLevel;
			double halfHeight = __instance.Bounds.InnerHeight / 2 / __instance.ZoomLevel;
			__instance.CurrentBlockViewBounds.Set(currentPos.X - halfWidth, 0, currentPos.Z - halfHeight, currentPos.X + halfWidth, 0, currentPos.Z + halfHeight);
		}
		else
			__instance.CurrentBlockViewBounds.Translate(diffX, 0, diffZ);
		___prevPlayerPos.Set(currentPos);
	}

	[HarmonyPatch("PostRenderInteractiveElements")]
	[HarmonyTranspiler]
	internal static IEnumerable<CodeInstruction> PostRenderInteractiveElements(IEnumerable<CodeInstruction> instructions) {
		// Find https://github.com/anegostudios/vsessentialsmod/blob/b447263a4860f52d92fd29f800f3f1fd8e905c6a/Systems/WorldMap/GuiElementMap.cs#L83-L102
		// Remove lines (modified and moved to RenderInteractiveElements())
		CodeMatcher matcher = new(instructions);
		int start = matcher.MatchStartForward([
			new(OpCodes.Ldarg_0),
			new(OpCodes.Ldfld, typeof(GuiElement).GetCheckedField("api", BindingFlags.Instance)),
			new(OpCodes.Callvirt, typeof(ICoreClientAPI).GetCheckedProperty("World", BindingFlags.Instance).CheckedGetMethod()),
			new(OpCodes.Callvirt, typeof(IClientWorldAccessor).GetCheckedProperty("Player", BindingFlags.Instance).CheckedGetMethod()),
			new(OpCodes.Callvirt, typeof(IPlayer).GetCheckedProperty("Entity", BindingFlags.Instance).CheckedGetMethod()),
		]).ThrowIfInvalid("Could not find `GuiElementMap.PostRenderInteractiveElements()::entityPlayer` to patch").Pos;
		matcher.MatchStartForward([
			new(OpCodes.Callvirt, typeof(Vec3d).GetCheckedMethod("Set", BindingFlags.Instance, [typeof(double), typeof(double), typeof(double)])),
			new(OpCodes.Pop),
			new(OpCodes.Ldarg_0),
			new(OpCodes.Call, typeof(GuiElementMap).GetCheckedProperty("dialogHasFocus", BindingFlags.Instance).CheckedGetMethod()),
		]).ThrowIfInvalid("Could not find `GuiElementMap.PostRenderInteractiveElements()::prevPlayerPos` to patch").RemoveInstructionsInRange(start, matcher.Pos + 1);
		return matcher.InstructionEnumeration();
	}
}
