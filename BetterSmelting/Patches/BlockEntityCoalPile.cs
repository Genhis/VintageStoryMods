using HarmonyLib;
using Vintagestory.API.Common;
using Vintagestory.GameContent;

namespace BetterSmelting.Patches {
	[HarmonyPatch(typeof(BlockEntityCoalPile))]
	internal static class BlockEntityCoalPilePatch {
		[HarmonyPatch("updateBurningState")]
		[HarmonyPostfix]
		internal static void UpdateBurningState(BlockEntityCoalPile __instance) {
			ItemStack? stack = __instance.inventory[0].Itemstack;
			if(stack == null)
				return;

			CombustibleProperties combustible = stack.Collectible.CombustibleProps;
			__instance.BurnHoursPerLayer = 4 * combustible.BurnDuration / 40 * combustible.BurnTemperature / 1300;
		}
	}
}
