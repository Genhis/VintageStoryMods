namespace Mapper;

using HarmonyLib;
using Mapper.GameContent;
using Mapper.Util;
using Mapper.Util.Harmony;
using Mapper.Util.IO;
using Mapper.Util.Reflection;
using Mapper.WorldMap;
using System;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.GameContent;

public class MapperModSystem : ModSystem {
	internal static bool enabled = true;
	private Harmony? harmony;
	private CompassNeedleUpdater? compassNeedleUpdater;
	internal MapperChunkMapLayer? mapLayer;

	public override void Start(ICoreAPI api) {
		base.Start(api);

		if(Harmony.HasAnyPatches(this.Mod.Info.ModID))
			this.Mod.Logger.Notification("Code is already patched");
		else if(MapperModSystem.enabled)
			this.PatchCode();

		this.Mod.Logger.Notification("Registering new classes");
		api.ModLoader.GetModSystem<WorldMapManager>().RegisterMapLayer<MapperChunkMapLayer>("chunks", 0);
		api.RegisterBlockClass("MapperCartographyTable", typeof(BlockCartographyTable));
		api.RegisterBlockEntityClass("MapperCartographyTable", typeof(BlockEntityCartographyTable));
		api.RegisterCollectibleBehaviorClass("MapperCartographyTableDisplay", typeof(BehaviorCartographyTableDisplay));
		api.RegisterCollectibleBehaviorClass("MapperCompassNeedle", typeof(BehaviorCompassNeedle));
		api.RegisterItemClass("MapperMap", typeof(ItemMap));
		api.RegisterItemClass("MapperPaintbrush", typeof(ItemPaintbrush));
	}

	public override void StartClientSide(ICoreClientAPI api) {
		base.StartClientSide(api);
		api.Event.LevelFinalize += this.OnLevelFinalizedClient;

		int count = api.Gui.Icons.RegisterCustomIcons(api, "textures/icons/gui", "mapper");
		this.Mod.Logger.Notification($"Registered {count} custom GUI icons");

		this.compassNeedleUpdater = new CompassNeedleUpdater(api);
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
			this.harmony.PatchDynamic(this.Mod.Logger);

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

	private void OnLevelFinalizedClient() {
		this.Mod.Logger.Notification($"Loaded {TesselationUtil.LoadedMeshCount} additional shapes and {CustomTextureSource.LoadedTextureCount} textures for dynamic effects");
		if(MapperModSystem.enabled)
			PatchDebugger.CheckPatchConflicts(this.Mod.Info.ModID, this.Mod.Logger, true);
		else
			this.Mod.Logger.Error("Patching failed, Mapper won't function properly");
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
