namespace TextInputEnhancements.Extensions;

using HarmonyLib;
using System;
using System.Reflection.Emit;

public static class HarmonyExtensions {
	public static CodeMatcher ReplaceConstructor(this CodeMatcher matcher, Type from, Type to, Type[] args, Type[] newArgs = null) {
		return matcher.MatchStartForward(new CodeMatch(OpCodes.Newobj, from.GetCheckedConstructor(args))).ThrowIfInvalid($"Constructor not found in IL code: {from.Name}({args.JoinNames()})").SetOperandAndAdvance(to.GetCheckedConstructor(newArgs ?? args));
	}
}
