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
using Vintagestory.API.Common.Entities;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;

[HarmonyPatch(typeof(EntityMapComponent))]
internal static class EntityMapComponentPatch {
	[HarmonyPatch("Render")]
	[HarmonyTranspiler]
	internal static IEnumerable<CodeInstruction> Render(IEnumerable<CodeInstruction> instructions, ILGenerator generator) {
		return EntityMapComponentPatch.SharedTranspiler(new CodeMatcher(instructions, generator), "Render", false).InstructionEnumeration();
	}

	[HarmonyPatch("OnMouseMove")]
	[HarmonyTranspiler]
	internal static IEnumerable<CodeInstruction> OnMouseMove(IEnumerable<CodeInstruction> instructions, ILGenerator generator) {
		return EntityMapComponentPatch.SharedTranspiler(new CodeMatcher(instructions, generator), "OnMouseMove", true).InstructionEnumeration();
	}

	private static CodeMatcher SharedTranspiler(CodeMatcher matcher, string functionName, bool localViewPos) {
		// Insert at the start:
		// int? scaleFactor = EntityMapComponentPatch.GetScaleFactor(this);
		// if(scaleFactor == null) return;
		matcher.Start().CreateLabel(out Label originalStart).DeclareLocal(typeof(int?), out LocalBuilder scaleFactor).InsertAndAdvance([
			new(OpCodes.Ldarg_0),
			CodeInstruction.Call(typeof(EntityMapComponentPatch), "GetScaleFactor"),
			CodeInstruction.StoreLocal(scaleFactor.LocalIndex),
			CodeInstruction.LoadLocal(scaleFactor.LocalIndex, true),
			new(OpCodes.Call, typeof(int?).GetCheckedProperty("HasValue", BindingFlags.Instance).CheckedGetMethod()),
			new(OpCodes.Brtrue_S, originalStart),
			new(OpCodes.Ret),
		]);

		// Find https://github.com/anegostudios/vsessentialsmod/blob/b447263a4860f52d92fd29f800f3f1fd8e905c6a/Systems/WorldMap/EntityLayer/EntityMapComponent.cs#L37 or
		// Find https://github.com/anegostudios/vsessentialsmod/blob/b447263a4860f52d92fd29f800f3f1fd8e905c6a/Systems/WorldMap/EntityLayer/EntityMapComponent.cs#L90
		// Change argument 1 to `MapperChunkMapLayer.ClampPosition(entity.Pos.XYZ, scaleFactor.Value)`
		List<CodeMatch> query = [
			new(OpCodes.Callvirt, typeof(EntityPos).GetCheckedProperty("XYZ", BindingFlags.Instance).CheckedGetMethod()),
			new(OpCodes.Callvirt, typeof(GuiElementMap).GetCheckedMethod("TranslateWorldPosToViewPos", BindingFlags.Instance, [typeof(Vec3d), typeof(Vec2f).MakeByRefType()])),
		];
		if(localViewPos)
			query.Insert(1, CodeMatch.IsLdloc());
		else
			query.InsertRange(1, [
				new(OpCodes.Ldarg_0),
				new(OpCodes.Ldflda, typeof(EntityMapComponent).GetCheckedField("viewPos", BindingFlags.Instance)),
			]);

		matcher.MatchStartForward(query.ToArray()).ThrowIfInvalid($"Could not find `EntityMapComponent.{functionName}::TranslateWorldPosToViewPos()`").Advance(1).InsertAndAdvance([
			CodeInstruction.LoadLocal(scaleFactor.LocalIndex, true),
			new(OpCodes.Call, typeof(int?).GetCheckedProperty("Value", BindingFlags.Instance).CheckedGetMethod()),
			CodeInstruction.Call(typeof(MapperChunkMapLayer), "ClampPosition"),
		]);
		return matcher;
	}

	internal static int? GetScaleFactor(EntityMapComponent component) {
		return MapperChunkMapLayer.GetInstance(component.capi).GetScaleFactor((component.entity as EntityPlayer)?.Player as IClientPlayer, component.entity.Pos.ToChunkPosition());
	}
}
