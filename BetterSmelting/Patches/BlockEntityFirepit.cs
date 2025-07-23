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

		[HarmonyPatch("OnSlotModifid")]
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

			// Find https://github.com/anegostudios/vssurvivalmod/blob/ac9a0059d84ca3449f066f26b5ee6b47bc9ce76a/BlockEntity/Firepit/BEFirepit.cs#L317
			// Append `* BlockEntityFirepitPatch.HeatInputItemStackMass(this)`
			matcher.MatchEndForward(new CodeMatch[] {
				new(OpCodes.Ldc_R4, 1.6f),
				new(OpCodes.Call, typeof(GameMath).GetMethod("Clamp", new System.Type[]{typeof(float), typeof(float), typeof(float)})),
				new(OpCodes.Add),
				new(OpCodes.Ldarg_1),
				new(OpCodes.Mul),
			}).ThrowIfInvalid("Could not find `BlockEntityFirepit.heatInput()::f` to patch").Advance(1).InsertAndAdvance(new CodeInstruction[] {
				new(OpCodes.Ldarg_0),
				CodeInstruction.Call(typeof(BlockEntityFirepitPatch), "HeatInputItemStackMass"),
				new(OpCodes.Div),
			});

			// Remember `f` set instruction
			CodeInstruction fStloc = matcher.Instruction;
			if(!fStloc.IsStloc())
				throw new System.InvalidOperationException("Unexpected instruction after `f` assignment expression, Stloc expected.");

			// Find https://github.com/anegostudios/vssurvivalmod/blob/ac9a0059d84ca3449f066f26b5ee6b47bc9ce76a/BlockEntity/Firepit/BEFirepit.cs#L318
			// Remove `f /= 11`
			matcher.MatchStartForward(new CodeMatch[] {
				new(CodeInstruction.LoadLocal(fStloc.LocalIndex())),
				new(OpCodes.Ldc_R4, 11f),
				new(OpCodes.Div),
				new(fStloc),
			}).ThrowIfInvalid("Could not find `f /= 11` in `BlockEntityFirepit.heatInput()` to patch").RemoveInstructions(4);

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
			return firepit.inputStack.StackSize + cookingMass * BlockEntityFirepitPatch.CookingSlotHeatingTimeMultiplier;
		}

		private static int GetSmeltedRatio(CollectibleObject obj) {
			return obj.Class == "ItemLiquidPortion" ? BlockEntityFirepitPatch.CookingSlotHeatingTimeLiquidSmeltedRatio : obj.CombustibleProps.SmeltedRatio;
		}

		[HarmonyPatch("smeltItems")]
		[HarmonyTranspiler]
		internal static IEnumerable<CodeInstruction> SmeltItems(IEnumerable<CodeInstruction> instructions) {
			// Find https://github.com/anegostudios/vssurvivalmod/blob/ac9a0059d84ca3449f066f26b5ee6b47bc9ce76a/BlockEntity/Firepit/BEFirepit.cs#L533
			// Remove line
			return new CodeMatcher(instructions).MatchStartForward(new CodeMatch[] {
				new(OpCodes.Ldarg_0),
				new(OpCodes.Ldarg_0),
				new(OpCodes.Callvirt, typeof(BlockEntityFirepit).GetMethod("environmentTemperature")),
				new(OpCodes.Conv_R4),
				new(OpCodes.Call, typeof(BlockEntityFirepit).GetProperty("InputStackTemp").SetMethod),
			}).ThrowIfInvalid("Could not find BlockEntityFirepit.smeltItems()::InputStackTemp to patch").RemoveInstructions(5).InstructionEnumeration();
		}
	}
}
