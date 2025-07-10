namespace TextInputEnhancements.Patches;

using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Reflection.Emit;
using TextInputEnhancements.Extensions;
using TextInputEnhancements.Gui;
using Vintagestory.API.Client;

[HarmonyPatch(typeof(GuiElementEditableTextBase))]
internal static class GuiElementEditableTextBasePatch {
	[HarmonyPatch("MoveCursor")]
	[HarmonyPrefix]
	internal static bool MoveCursor(GuiElementEditableTextBase __instance, int dir, bool wholeWord) {
		if(!wholeWord)
			return true;

		__instance.MoveCursorWholeWord(dir, false);
		return false;
	}

	internal static void MoveCursorWholeWord(this GuiElementEditableTextBase instance, int dir, bool forDeletion) {
		if(dir != 1 && dir != -1)
			throw new InvalidOperationException("Invalid direction for cursor movement: " + dir);

		static bool IsWordChar(char c) => c == '_' || char.IsLetterOrDigit(c);

		string text = instance.GetText();
		int stop = dir < 0 ? -1 : text.Length;
		int offset = dir < 0 ? -1 : 0;
		int caretPos = instance.CaretPosWithoutLineBreaks + offset;
		bool startsWithWhitespace = caretPos != stop && char.IsWhiteSpace(text[caretPos]);
		while(caretPos != stop && char.IsWhiteSpace(text[caretPos]))
			caretPos += dir;

		if(caretPos != stop && !IsWordChar(text[caretPos]))
			while(caretPos != stop && !IsWordChar(text[caretPos]) && !char.IsWhiteSpace(text[caretPos]))
				caretPos += dir;
		else
			while(caretPos != stop && IsWordChar(text[caretPos]))
				caretPos += dir;

		if(!startsWithWhitespace && forDeletion)
			while(caretPos != stop && char.IsWhiteSpace(text[caretPos]))
				caretPos += dir;

		instance.CaretPosWithoutLineBreaks = caretPos - offset;
	}

	[HarmonyPatch("OnFocusGained")]
	[HarmonyTranspiler]
	internal static IEnumerable<CodeInstruction> OnFocusGained(IEnumerable<CodeInstruction> instructions) {
		// Find https://github.com/anegostudios/vsapi/blob/3c751124b782707d4ae5cfb2d3265d2ea1eb805a/Client/UI/Elements/Impl/Interactive/Text/GuiElementEditableTextBase.cs#L148
		// Remove line
		return new CodeMatcher(instructions).MatchStartForward(new CodeMatch[] {
			new(OpCodes.Ldarg_0),
			new(OpCodes.Ldarg_0),
			new(OpCodes.Call, typeof(GuiElementEditableTextBase).GetCheckedProperty("TextLengthWithoutLineBreaks").CheckedGetMethod()),
			new(OpCodes.Ldc_I4_0),
			new(OpCodes.Call, typeof(GuiElementEditableTextBase).GetCheckedMethod("SetCaretPos", new[] {typeof(int), typeof(int)})),
		}).ThrowIfInvalid("Could not find `GuiElementEditableTextBase.OnFocusGained()::SetCaretPos()` to patch").RemoveInstructions(5).InstructionEnumeration();
	}

	[HarmonyPatch("TextChanged")]
	[HarmonyPostfix]
	internal static void TextChanged(GuiElementEditableTextBase __instance) {
		if(__instance is ITextEnhancements getter)
			getter.Enhancements.OnTextChanged();
	}
}
