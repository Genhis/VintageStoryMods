namespace Mapper;

using HarmonyLib;
using Mapper.Items;
using Mapper.Util.Reflection;
using System;
using Vintagestory.API.Common;

public class MapperModSystem : ModSystem {
	private static bool enabled = true;
	private Harmony? harmony;

	public override void Start(ICoreAPI api) {
		base.Start(api);

		if(Harmony.HasAnyPatches(this.Mod.Info.ModID))
			this.Mod.Logger.Notification("Code is already patched");
		else if(MapperModSystem.enabled)
			this.PatchCode();

		if(MapperModSystem.enabled) {
			api.RegisterItemClass("MapperMap", typeof(ItemMap));
			api.RegisterItemClass("MapperPaintbrush", typeof(ItemPaintbrush));
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
		base.Dispose();
	}
}
