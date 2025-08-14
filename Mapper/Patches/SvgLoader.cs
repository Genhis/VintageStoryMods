namespace Mapper.Patches;

using Cairo;
using HarmonyLib;
using Mapper.Util.Reflection;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.Client.NoObf;

[HarmonyPatch(typeof(SvgLoader))]
public static class SvgLoaderPatch {
	private static readonly FieldAccessor<GuiAPI, SvgLoader> guiApiSvgLoader = new("svgLoader");

	[HarmonyPatch("DrawSvg", [typeof(IAsset), typeof(ImageSurface), typeof(int), typeof(int), typeof(int), typeof(int), typeof(int?)])]
	[HarmonyReversePatch]
	public static void DrawSvgRGB(this SvgLoader instance, IAsset svgAsset, ImageSurface intoSurface, int posx, int posy, int width, int height, int? color) {
		static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, ILGenerator generator) {
			// Swap red and blue channels of the source data coming from the rasterizer
			// Fix https://github.com/anegostudios/VintageStory-Issues/issues/6278
			return new CodeMatcher(instructions, generator).MatchStartForward([
				CodeMatch.IsLdloc(),
				CodeMatch.IsLdloc(),
				new(OpCodes.Call, typeof(ColorUtil).GetCheckedMethod("ColorOver", BindingFlags.Static, [typeof(int), typeof(int)])),
			]).ThrowIfInvalid("Could not find `SvgLoader.DrawSvg()::ColorOver(src, dst)` to patch").Advance(1).CreateLabel(out Label skipReverseColorBytes).InsertAndAdvance([
				new(OpCodes.Ldarga_S, (sbyte)7),
				new(OpCodes.Call, typeof(int?).GetCheckedProperty("HasValue", BindingFlags.Instance).CheckedGetMethod()),
				new(OpCodes.Brtrue_S, skipReverseColorBytes),
				CodeInstruction.Call(typeof(ColorUtil), "ReverseColorBytes", [typeof(int)]),
			]).InstructionEnumeration();
		}
		Transpiler(null!, null!);
	}

	public static void DrawSvgRGB(this IGuiAPI iGuiApi, IAsset svgAsset, ImageSurface intoSurface, int posx, int posy, int width = 0, int height = 0, int? color = 0) {
		if(iGuiApi is not GuiAPI guiApi)
			throw new NotSupportedException("This implementation of IGuiAPI is not supported");
		SvgLoaderPatch.guiApiSvgLoader.GetValue(guiApi).DrawSvgRGB(svgAsset, intoSurface, posx, posy, width, height, color);
	}
}
