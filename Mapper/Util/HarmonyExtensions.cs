namespace Mapper.Util;

using HarmonyLib;

public static class HarmonyExtensions {
	public static CodeMatcher InsertAndAdvanceTransferLabels(this CodeMatcher matcher, params CodeInstruction[] instructions) {
		return matcher.TransferLabels(instructions[0]).InsertAndAdvance(instructions);
	}

	public static CodeMatcher TransferLabels(this CodeMatcher matcher, CodeInstruction instruction) {
		if(matcher.Instruction.labels.Count > 0)
			(instruction.labels, matcher.Instruction.labels) = (matcher.Instruction.labels, instruction.labels);
		return matcher;
	}
}
