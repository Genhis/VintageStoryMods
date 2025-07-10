namespace TextInputEnhancements;

using HarmonyLib;
using System;
using System.Reflection;
using Vintagestory.API.Common;

internal class HarmonyPatcher : ModSystem {
	private Harmony harmony;

	public override void Start(ICoreAPI api) {
		base.Start(api);

		this.Mod.Logger.Notification("Preparing reflection accessors");
		foreach(Type t in Assembly.GetExecutingAssembly().GetTypes())
			foreach(FieldInfo field in t.GetFields(BindingFlags.NonPublic | BindingFlags.Static))
				if(field.FieldType == typeof(FieldInfo) && field.GetValue(null) == null) {
					this.Mod.Logger.Error($"An error occured, disabling mod: {t.Name}.{field.Name} is null");
					return;
				}

		this.Mod.Logger.Notification("Patching code");
		this.harmony = new Harmony(this.Mod.Info.ModID);
		try {
			this.harmony.PatchAll();
		}
		catch(Exception ex) {
			this.Mod.Logger.Error("An error occured, disabling mod:\n" + ex.ToString());
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
