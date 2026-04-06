using HarmonyLib;
using System.Collections.Generic;
using System.Reflection.Emit;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;

namespace BetterSmelting.Patches {
	[HarmonyPatch(typeof(BlockEntityFirepit))]
	internal static class BlockEntityFirepitPatch {
		internal static int CookingSlotHeatingTimeLiquidSmeltedRatio = 0;
		internal static float CookingSlotHeatingTimeMultiplier = 0;

		[HarmonyPatch("OnSlotModified")]
		[HarmonyPostfix]
		internal static void OnSlotModifid(BlockEntityFirepit __instance) {
			if(__instance.otherCookingSlots.Length == 0)
				return;

			// Average temperature between all slots in cooking containers.
			double sumTemp = 0;
			int stackSize = 0;
			foreach(ItemSlot slot in __instance.otherCookingSlots)
				if(!slot.Empty) {
					ItemStack stack = slot.Itemstack;
					sumTemp += (double)stack.Collectible.GetTemperature(__instance.Api.World, stack) * stack.StackSize;
					stackSize += stack.StackSize;
				}
			if(stackSize == 0)
				return;

			float newTemp = (float)(sumTemp / stackSize);
			foreach(ItemSlot slot in __instance.otherCookingSlots)
				if(!slot.Empty)
					slot.Itemstack.Collectible.SetTemperature(__instance.Api.World, slot.Itemstack, newTemp, false);
		}

		[HarmonyPatch("heatInput")]
		[HarmonyTranspiler]
		internal static IEnumerable<CodeInstruction> HeatInput(IEnumerable<CodeInstruction> instructions) {
			CodeMatcher matcher = new(instructions);

			// Find https://github.com/anegostudios/vssurvivalmod/blob/8eb9552972540749393fa7fa8206d28f582e8dca/BlockEntity/Firepit/BEFirepit.cs#L338
			// Append `+ BlockEntityFirepitPatch.HeatInputItemStackMass(this)`
			matcher.MatchEndForward([
				new(OpCodes.Callvirt, typeof(ItemStack).GetProperty("StackSize")!.GetMethod),
				new(OpCodes.Conv_R4),
				CodeMatch.IsStloc(),
			]).ThrowIfInvalid("Could not find `BlockEntityFirepit.heatInput()::stackSize` to patch").InsertAndAdvance([
				new(OpCodes.Ldarg_0),
				CodeInstruction.Call(typeof(BlockEntityFirepitPatch), "HeatInputItemStackMass"),
				new(OpCodes.Add),
			]);

			// Find https://github.com/anegostudios/vssurvivalmod/blob/8eb9552972540749393fa7fa8206d28f582e8dca/BlockEntity/Firepit/BEFirepit.cs#L344
			// Remove `if(nowTemp >= meltingPoint) f /= 11`
			int fLocalIndex = -1;
			matcher.MatchStartForward([
				new(i => {
					if(!i.IsStloc())
						return false;
					fLocalIndex = i.LocalIndex();
					return true;
				}),
				CodeMatch.IsLdloc(),
				CodeMatch.IsLdloc(),
				new(i => matcher.Remaining >= 8 && i.opcode == OpCodes.Blt_Un_S && i.operand is Label label && matcher.InstructionAt(8).labels.Contains(label)),
				new(i => i.IsLdloc() && i.LocalIndex() == fLocalIndex),
				new(OpCodes.Ldc_R4, 11f),
				new(OpCodes.Div),
				new(i => i.IsStloc() && i.LocalIndex() == fLocalIndex),
			]).ThrowIfInvalid("Could not find `f /= 11` in `BlockEntityFirepit.heatInput()` to patch").Advance(1).RemoveInstructions(7);

			// Find https://github.com/anegostudios/vssurvivalmod/blob/ac9a0059d84ca3449f066f26b5ee6b47bc9ce76a/BlockEntity/Firepit/BEFirepit.cs#L338
			// Change Clamp() arguments to float type
			matcher.MatchStartForward(new CodeMatch[] {
				new(OpCodes.Conv_I4),
				new(OpCodes.Ldc_I4_1),
				new(OpCodes.Ldc_I4_S, (sbyte)30),
				new(OpCodes.Call, typeof(GameMath).GetMethod("Clamp", new System.Type[]{typeof(int), typeof(int), typeof(int)})),
				new(OpCodes.Conv_R4),
			}).ThrowIfInvalid("Could not find `BlockEntityFirepit.heatInput()::diff` to patch").RemoveInstructions(5).InsertAndAdvance(new CodeInstruction[] {
				new(OpCodes.Ldc_R4, 1f),
				new(OpCodes.Ldc_R4, 30f),
				CodeInstruction.Call(typeof(GameMath), "Clamp", new System.Type[]{typeof(float), typeof(float), typeof(float)}),
			});

			return matcher.InstructionEnumeration();
		}

		internal static float HeatInputItemStackMass(BlockEntityFirepit firepit) {
			float cookingMass = 0;
			foreach(ItemSlot slot in firepit.otherCookingSlots)
				if(slot.Itemstack?.Collectible?.CombustibleProps != null)
					cookingMass += slot.Itemstack.StackSize / BlockEntityFirepitPatch.GetSmeltedRatio(slot.Itemstack.Collectible);
			return cookingMass * BlockEntityFirepitPatch.CookingSlotHeatingTimeMultiplier;
		}

		private static int GetSmeltedRatio(CollectibleObject obj) {
			return obj.Class == "ItemLiquidPortion" ? BlockEntityFirepitPatch.CookingSlotHeatingTimeLiquidSmeltedRatio : obj.CombustibleProps.SmeltedRatio;
		}
	}
}
