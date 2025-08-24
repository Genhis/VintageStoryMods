namespace Mapper.Util.Harmony;

using HarmonyLib;
using System.Reflection;
using Vintagestory.API.Common;

public static class HarmonyExtensions {
	public static void PatchDynamic(this Harmony harmony, ILogger logger) {
		new DynamicPatchResolver(harmony, logger).Patch(Assembly.GetCallingAssembly());
	}

	public static void UnpatchDynamic(this Harmony harmony, string assemblyName) {
		DynamicPatchResolver.Unpatch(harmony, assemblyName);
	}

	public static CodeMatcher InsertAndAdvanceTransferLabels(this CodeMatcher matcher, params CodeInstruction[] instructions) {
		return matcher.TransferLabels(instructions[0]).InsertAndAdvance(instructions);
	}

	public static CodeMatcher TransferLabels(this CodeMatcher matcher, CodeInstruction instruction) {
		if(matcher.Instruction.labels.Count > 0)
			(instruction.labels, matcher.Instruction.labels) = (matcher.Instruction.labels, instruction.labels);
		return matcher;
	}
}
