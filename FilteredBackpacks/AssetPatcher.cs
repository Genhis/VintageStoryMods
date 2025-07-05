namespace FilteredBackpacks;

using System;
using System.Collections.Generic;
using Vintagestory.API.Common;
using Vintagestory.GameContent;

public class AssetPatcher : ModSystem {
	public override void AssetsFinalize(ICoreAPI api) {
		base.AssetsFinalize(api);
		if(api.Side != EnumAppSide.Server)
			return;

		ServerConfig config = this.LoadServerConfig(api);
		foreach(BagPatch patch in config.bags)
			patch.Prepare();
		if(config.addAgricultureItemsBasedOnBlockDrops)
			this.AddAgricultureCodesFromDrops(api, config.agriculture.codes);
		if(config.revertBaseGameMiningStorageFlags)
			this.RevertBaseGameMiningStorageFlags(api);

		this.Mod.Logger.Notification("Patching assets to add filtered storage flags");
		foreach(CollectibleObject obj in api.World.Collectibles)
			if(obj != null && obj.Code != null)
				foreach(BagPatch patch in config.bags)
					patch.CheckAndApply(obj);
	}

	private ServerConfig LoadServerConfig(ICoreAPI api) {
		this.Mod.Logger.Notification("Loading server config");
		const string configPath = "FilteredBackpacks.json";
		const int configVersion = 1;
		try {
			ServerConfig config = api.LoadModConfig<ServerConfig>(configPath);
			if(config == null || config.refreshConfigWhenVersionChanges && config.configVersion != configVersion)
				config = new ServerConfig(configVersion);
			api.StoreModConfig(config, configPath);
			return config;
		}
		catch(Exception ex) {
			this.Mod.Logger.Error("An error occured while loading the server config, using default settings instead:\n" + ex.ToString());
			return new ServerConfig(0);
		}
	}

	private void AddAgricultureCodesFromDrops(ICoreAPI api, HashSet<string> agricultureCodes) {
		int originalCount = agricultureCodes.Count;
		foreach(Block block in api.World.Blocks) {
			if(block == null || block.Code == null)
				continue;

			if(block.Class == "BlockCrop" || block.Class == "BlockBerryBush")
				foreach(BlockDropItemStack drop in block.Drops)
					agricultureCodes.Add(drop.Code.ToString());
			else if(block.Class == "BlockDynamicTreeBranch")
				foreach(KeyValuePair<string, FruitTreeTypeProperties> fruitType in block.Attributes["fruittreeProperties"]?.AsObject<Dictionary<string, FruitTreeTypeProperties>>() ?? new())
					foreach(BlockDropItemStack drop in fruitType.Value.FruitStacks)
						agricultureCodes.Add(drop.Code.ToString());
		}
		this.Mod.Logger.Notification($"Added {agricultureCodes.Count - originalCount} agriculture items based on block drops");
	}

	private void RevertBaseGameMiningStorageFlags(ICoreAPI api) {
		int count = 0;
		foreach(CollectibleObject obj in api.World.Collectibles)
			if((obj.StorageFlags & EnumItemStorageFlags.Metallurgy) != 0) {
				obj.StorageFlags &= ~EnumItemStorageFlags.Metallurgy;
				++count;
			}
		this.Mod.Logger.Notification($"Removed Metallurgy storage flag from {count} items");
	}
}
