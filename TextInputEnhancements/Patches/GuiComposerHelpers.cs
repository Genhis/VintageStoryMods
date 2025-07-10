namespace TextInputEnhancements.Patches;

using HarmonyLib;
using System;
using System.Collections.Generic;
using TextInputEnhancements.Extensions;
using TextInputEnhancements.Gui;
using Vintagestory.API.Client;

[HarmonyPatch(typeof(GuiComposerHelpers))]
internal static class GuiComposerHelpersPatch {
	[HarmonyPatch("AddChatInput")]
	[HarmonyTranspiler]
	internal static IEnumerable<CodeInstruction> AddChatInput(IEnumerable<CodeInstruction> instructions) {
		return new CodeMatcher(instructions).ReplaceConstructor(typeof(GuiElementChatInput), typeof(GuiElementEnhancedChatInput), new[] {typeof(ICoreClientAPI), typeof(ElementBounds), typeof(Action<string>)}).InstructionEnumeration();
	}

	[HarmonyPatch("AddTextArea")]
	[HarmonyTranspiler]
	internal static IEnumerable<CodeInstruction> AddTextArea(IEnumerable<CodeInstruction> instructions) {
		return new CodeMatcher(instructions).ReplaceConstructor(typeof(GuiElementTextArea), typeof(GuiElementEnhancedTextArea), new[] {typeof(ICoreClientAPI), typeof(ElementBounds), typeof(Action<string>), typeof(CairoFont)}).InstructionEnumeration();
	}

	[HarmonyPatch("AddTextInput")]
	[HarmonyTranspiler]
	internal static IEnumerable<CodeInstruction> AddTextInput(IEnumerable<CodeInstruction> instructions) {
		return new CodeMatcher(instructions).ReplaceConstructor(typeof(GuiElementTextInput), typeof(GuiElementEnhancedTextInput), new[] {typeof(ICoreClientAPI), typeof(ElementBounds), typeof(Action<string>), typeof(CairoFont)}).InstructionEnumeration();
	}
}
