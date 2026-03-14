namespace Mapper.Util.Harmony;

using HarmonyLib;

public static class HarmonyUtil {
	public static CodeMatch IsLdloc(int localIndex) {
		return new CodeMatch(instruction => instruction.IsLdloc() && instruction.LocalIndex() == localIndex);
	}

	public static CodeMatch IsStloc(int localIndex) {
		return new CodeMatch(instruction => instruction.IsStloc() && instruction.LocalIndex() == localIndex);
	}
}
