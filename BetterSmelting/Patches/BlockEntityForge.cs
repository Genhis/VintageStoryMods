using HarmonyLib;
using System.Collections.Generic;
using System.Reflection.Emit;
using Vintagestory.API.Common;
using Vintagestory.GameContent;

namespace BetterSmelting.Patches {
	[HarmonyPatch(typeof(BlockEntityForge))]
	internal static class BlockEntityForgePatch {
		private const float FuelValuePerCharcoal = 0.0625f;

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

			// Find https://github.com/anegostudios/vssurvivalmod/blob/ac9a0059d84ca3449f066f26b5ee6b47bc9ce76a/BlockEntity/BEForge.cs#L183
			// Replace the condition with `if(!BlockEntityForgePatch.AddFuel(ref this.fuelLevel, combprops))`
			matcher.MatchStartForward(new CodeMatch[] {
				new(OpCodes.Ldarg_0),
				new(OpCodes.Ldfld, typeof(BlockEntityForge).GetField("fuelLevel")),
				new(OpCodes.Ldc_R4, FuelValuePerCharcoal * 5),
				new(OpCodes.Blt_Un_S),
			}).ThrowIfInvalid("Could not find `if(fuelLevel >= 0.3125f)` in `BlockEntityForge.OnPlayerInteract()` to patch");
			matcher.Advance(1).SetOpcodeAndAdvance(OpCodes.Ldflda).InsertAndAdvance(new CodeInstruction[] {
				combpropsLdloc.Clone(),
				CodeInstruction.Call(typeof(BlockEntityForgePatch), "AddFuel"),
			}).RemoveInstruction().SetOpcodeAndAdvance(OpCodes.Brtrue_S);

			// Find https://github.com/anegostudios/vssurvivalmod/blob/ac9a0059d84ca3449f066f26b5ee6b47bc9ce76a/BlockEntity/BEForge.cs#L184
			// Remove line
			List<Label> labels = matcher.MatchStartForward(new CodeMatch[] {
				new(OpCodes.Ldarg_0),
				new(OpCodes.Ldarg_0),
				new(OpCodes.Ldfld, typeof(BlockEntityForge).GetField("fuelLevel")),
				new(OpCodes.Ldc_R4, FuelValuePerCharcoal),
				new(OpCodes.Add),
				new(OpCodes.Stfld, typeof(BlockEntityForge).GetField("fuelLevel")),
			}).ThrowIfInvalid("Could not find `BlockEntityForge.OnPlayerInteract()::fuelLevel` to patch").Instruction.labels;
			matcher.RemoveInstructions(6);
			if(labels.Count > 0)
				matcher.Instruction.labels.AddRange(labels);

			return matcher.InstructionEnumeration();
		}

		internal static bool AddFuel(ref float currentFuel, CombustibleProperties combustible) {
			float addedFuel = FuelValuePerCharcoal * combustible.BurnDuration / 40 * combustible.BurnTemperature / 1300;
			if(currentFuel != 0 && currentFuel + addedFuel > FuelValuePerCharcoal * 5.8f)
				return false;

			currentFuel += addedFuel;
			return true;
		}
	}
}
