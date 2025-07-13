using BetterSmelting.Patches;
using HarmonyLib;
using System;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Server;

namespace BetterSmelting {
	public class CoreModSystem : ModSystem {
		private static bool enabled = true;
		private Harmony harmony;

		public override void Start(ICoreAPI api) {
			base.Start(api);
			if(!CoreModSystem.enabled)
				return;

			if(Harmony.HasAnyPatches(this.Mod.Info.ModID)) {
				this.Mod.Logger.Notification("Code is already patched");
				return;
			}

			this.Mod.Logger.Notification("Patching code");
			this.harmony = new Harmony(this.Mod.Info.ModID);
			try {
				this.harmony.PatchAll();
			}
			catch(Exception ex) {
				this.Mod.Logger.Error("An error occured while patching code, disabling mod:\n" + ex.ToString());
				this.harmony.UnpatchAll(this.harmony.Id);
				this.harmony = null;
				CoreModSystem.enabled = false;
			}
		}

		public override void StartClientSide(ICoreClientAPI api) {
			base.StartClientSide(api);
			if(!CoreModSystem.enabled)
				return;

			this.Mod.Logger.Notification("Retrieving server config for the client");
			BlockEntityFirepitPatch.CookingSlotHeatingTimeMultiplier = api.World.Config.GetFloat(this.Mod.Info.ModID + "_CookingSlotHeatingTimeMultiplier");
			BlockEntityForgePatch.ForgeMinimumFuelTemperature = api.World.Config.GetInt(this.Mod.Info.ModID + "_ForgeMinimumFuelTemperature");
		}

		public override void StartServerSide(ICoreServerAPI api) {
			base.StartServerSide(api);
			if(!CoreModSystem.enabled)
				return;

			this.LoadServerConfig(api);
			api.World.Config.SetFloat(this.Mod.Info.ModID + "_CookingSlotHeatingTimeMultiplier", BlockEntityFirepitPatch.CookingSlotHeatingTimeMultiplier);
			api.World.Config.SetInt(this.Mod.Info.ModID + "_ForgeMinimumFuelTemperature", BlockEntityForgePatch.ForgeMinimumFuelTemperature);
		}

		public override void Dispose() {
			if(this.harmony != null) {
				this.Mod.Logger.Notification("Unpatching code");
				this.harmony.UnpatchAll(this.harmony.Id);
				this.harmony = null;
			}
			base.Dispose();
		}

		private void LoadServerConfig(ICoreServerAPI api) {
			this.Mod.Logger.Notification("Loading server config");
			const string configPath = "BetterSmelting.json";
			ServerConfig config;
			try {
				config = api.LoadModConfig<ServerConfig>(configPath) ?? new ServerConfig();
				api.StoreModConfig(config, configPath);
			}
			catch(Exception ex) {
				this.Mod.Logger.Error("An error occured while loading the server config, using default settings instead:\n" + ex.ToString());
				config = new ServerConfig();
			}

			BlockEntityFirepitPatch.CookingSlotHeatingTimeMultiplier = config.CookingSlotHeatingTimeMultiplier;
			BlockEntityForgePatch.ForgeMinimumFuelTemperature = config.ForgeMinimumFuelTemperature;
		}
	}
}
