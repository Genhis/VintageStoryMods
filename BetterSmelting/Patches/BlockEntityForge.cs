using HarmonyLib;
using System.Collections.Generic;
using System.Reflection.Emit;
using Vintagestory.API.Common;
using Vintagestory.GameContent;

namespace BetterSmelting.Patches {
	[HarmonyPatch(typeof(BlockEntityForge))]
	internal static class BlockEntityForgePatch {
		[HarmonyPatch("OnPlayerInteract")]
		[HarmonyTranspiler]
		internal static IEnumerable<CodeInstruction> OnPlayerInteract(IEnumerable<CodeInstruction> instructions) {
			CodeMatcher matcher = new(instructions);

			// Find https://github.com/anegostudios/vssurvivalmod/blob/ac9a0059d84ca3449f066f26b5ee6b47bc9ce76a/BlockEntity/BEForge.cs#L181
			matcher.MatchStartForward(new CodeMatch[] {
				new(OpCodes.Ldfld, typeof(CombustibleProperties).GetField("BurnTemperature")),
				new(OpCodes.Ldc_I4, 1000),
			}).ThrowIfInvalid("Could not find `BurnTemperature > 1000` in `BlockEntityForge.OnPlayerInteract()`");

			// Remember `combprops` load instruction
			CodeInstruction combpropsLdloc = matcher.InstructionAt(-1);
			if(!combpropsLdloc.IsLdloc())
				throw new System.InvalidOperationException("Ldfld(CombustibleProperties.BurnTemperature) is not preceeded by Ldloc instruction");

			// Find https://github.com/anegostudios/vssurvivalmod/blob/ac9a0059d84ca3449f066f26b5ee6b47bc9ce76a/BlockEntity/BEForge.cs#L184
			// Replace constant operand with `1 / 16f / 40 / 1300`
			// Append `* combprops.BurnDuration * combprops.BurnTemperature`
			matcher.MatchStartForward(new CodeMatch[] {
				new(OpCodes.Ldfld, typeof(BlockEntityForge).GetField("fuelLevel")),
				new(OpCodes.Ldc_R4, 0.0625f),
				new(OpCodes.Add),
				new(OpCodes.Stfld, typeof(BlockEntityForge).GetField("fuelLevel")),
			}).ThrowIfInvalid("Could not find `BlockEntityForge.OnPlayerInteract()::fuelLevel` to patch")
			.Advance(1).SetOperandAndAdvance(0.0625f / 40 / 1300).InsertAndAdvance(new CodeInstruction[] {
				combpropsLdloc.Clone(),
				CodeInstruction.LoadField(typeof(CombustibleProperties), "BurnDuration"),
				new(OpCodes.Mul),
				combpropsLdloc.Clone(),
				CodeInstruction.LoadField(typeof(CombustibleProperties), "BurnTemperature"),
				new(OpCodes.Mul),
			});

			return matcher.InstructionEnumeration();
		}
	}
}
