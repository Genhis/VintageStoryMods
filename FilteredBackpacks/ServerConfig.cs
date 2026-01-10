namespace FilteredBackpacks;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Collections.Generic;
using Vintagestory.API.Common;

public class BagPatch {
	public HashSet<string> classes = new();
	public HashSet<string> codes = new();
	public EnumItemStorageFlags storageFlags;

	private readonly string bagType;
	private AssetLocation[] wildcards = [];

	public BagPatch(EnumItemStorageFlags storageFlags, string bagType) {
		this.storageFlags = storageFlags;
		this.bagType = bagType;
	}

	public void Prepare() {
		List<AssetLocation> wildcards = new();
		this.codes.RemoveWhere(code => {
			if(!code.Contains('*'))
				return false;

			wildcards.Add(new AssetLocation(code));
			return true;
		});
		this.wildcards = wildcards.ToArray();
	}

	internal void CheckAndApply(CollectibleObject obj) {
		if(this.classes.Contains(obj.Class) || this.codes.Contains(obj.Code.ToString()) || obj.WildCardMatch(this.wildcards))
			obj.StorageFlags |= this.storageFlags;
		if(obj.Code.BeginsWith("filteredbackpacks", this.bagType))
			obj.Attributes["backpack"].Token["storageFlags"] = JToken.FromObject(this.storageFlags);
	}
}

public class ServerConfig {
	[JsonProperty("_configVersion")]
	public int configVersion;
	public bool refreshConfigWhenVersionChanges = true; // The config is overwritten when config version does not match the latest.
	public bool addAgricultureItemsBasedOnBlockDrops = true;
	public bool revertBaseGameMiningStorageFlags = false;

	public readonly BagPatch agriculture = new(EnumItemStorageFlags.Agriculture, "bag-agriculture");
	public readonly BagPatch forestry = new(EnumItemStorageFlags.Custom2, "bag-forestry");
	public readonly BagPatch mining = new(EnumItemStorageFlags.Metallurgy, "bag-mining");

	[JsonIgnore]
	public readonly BagPatch[] bags;

	public ServerConfig() {
		this.bags = new[] {
			this.agriculture,
			this.mining,
			this.forestry,
		};
	}

	public ServerConfig(int configVersion) : this() {
		this.configVersion = configVersion;

		this.agriculture.classes = new() {
			"BlockDynamicTreeBranch",
			"BlockMushroom",
			"ItemCattailRoot",
			"ItemDryGrass",
			"ItemHoe",
			"ItemScythe",
		};
		this.agriculture.codes = new() {
			"game:cattailtops",
			"game:flaxfibers",
			"game:hay-normal-*",
			"game:papyrustops",
			"game:thatch",
		};

		this.forestry.classes = new() {
			"BlockLog",
			"BlockLogSection",
			"ItemAxe",
			"ItemShears",
			"ItemTreeSeed",
		};
		this.forestry.codes = new() {
			"game:firewood",
			"game:logquad-*",
			"game:saw-*",
			"game:stick",
		};
	}
}
