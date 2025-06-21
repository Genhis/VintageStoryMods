using HarmonyLib;
using Vintagestory.API.Common;
using Vintagestory.GameContent;

namespace BetterSmelting.Patches {
	[HarmonyPatch(typeof(BlockEntityCoalPile))]
	internal static class BlockEntityCoalPilePatch {
		[HarmonyPatch("updateBurningState")]
		[HarmonyPostfix]
		internal static void UpdateBurningState(BlockEntityCoalPile __instance) {
			if(__instance.inventory[0].Itemstack == null)
				return;

			CombustibleProperties combustible = __instance.inventory[0].Itemstack.Collectible.CombustibleProps;
			__instance.BurnHoursPerLayer = 4 * combustible.BurnDuration / 40 * combustible.BurnTemperature / 1300;
		}
	}
}
