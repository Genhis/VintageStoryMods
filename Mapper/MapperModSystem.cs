namespace Mapper;

using HarmonyLib;
using Mapper.Behaviors;
using Mapper.Items;
using Mapper.Util.IO;
using Mapper.Util.Reflection;
using Mapper.WorldMap;
using System;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.GameContent;

public class MapperModSystem : ModSystem {
	private static bool enabled = true;
	private Harmony? harmony;
	private CompassNeedleUpdater? compassNeedleUpdater;
	internal MapperChunkMapLayer? mapLayer;

	public override void Start(ICoreAPI api) {
		base.Start(api);

		if(Harmony.HasAnyPatches(this.Mod.Info.ModID))
			this.Mod.Logger.Notification("Code is already patched");
		else if(MapperModSystem.enabled)
			this.PatchCode();

		if(MapperModSystem.enabled) {
			this.Mod.Logger.Notification("Registering new classes");
			api.ModLoader.GetModSystem<WorldMapManager>().RegisterMapLayer<MapperChunkMapLayer>("chunks", 0);
			api.RegisterCollectibleBehaviorClass("MapperCompassNeedle", typeof(BehaviorCompassNeedle));
			api.RegisterItemClass("MapperMap", typeof(ItemMap));
			api.RegisterItemClass("MapperPaintbrush", typeof(ItemPaintbrush));
		}
	}

	public override void StartClientSide(ICoreClientAPI api) {
		base.StartClientSide(api);
		if(MapperModSystem.enabled) {
			this.Mod.Logger.Notification("Registering OnTick handler for compass updates");
			this.compassNeedleUpdater = new CompassNeedleUpdater(api);
		}
	}

	private void PatchCode() {
		this.Mod.Logger.Notification("Preparing reflection accessors");
		if(ReflectionAccessors.CheckErrors(this.Mod.Logger)) {
			MapperModSystem.enabled = false;
			return;
		}

		this.Mod.Logger.Notification("Patching code");
		this.harmony = new Harmony(this.Mod.Info.ModID);
		try {
			this.harmony.PatchAll();

			this.Mod.Logger.Notification("Testing save/load consistency");
			SaveLoadTests.Run();
		}
		catch(Exception ex) {
			this.Mod.Logger.Error("An error occured, disabling mod:\n" + ex.ToString());
			MapperModSystem.enabled = false;
			this.harmony.UnpatchAll(this.harmony.Id);
			this.harmony = null;
		}
	}

	public override void Dispose() {
		if(this.harmony != null) {
			this.Mod.Logger.Notification("Unpatching code");
			this.harmony.UnpatchAll(this.harmony.Id);
			this.harmony = null;
		}
		if(this.compassNeedleUpdater != null) {
			this.compassNeedleUpdater.Dispose();
			this.compassNeedleUpdater = null;
		}
		base.Dispose();
	}
}
