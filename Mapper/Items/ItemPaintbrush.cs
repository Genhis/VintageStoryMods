namespace Mapper.Items;

using Mapper.Util;
using System;
using System.Collections.Generic;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.Util;

public class ItemPaintbrush : Item {
	private SkillItem[]? toolModes;
	private bool hasUpgradeMode;
	private int minRange;
	private int stepRange;
	private int rangeCount;
	private int toolModeCount;

	public override void OnLoaded(ICoreAPI api) {
		base.OnLoaded(api);

		JsonObject input = this.GetMapperAttributes();
		ILogger logger = api.Logger;
		this.hasUpgradeMode = input["upgradeMode"].AsBool(true);
		this.minRange = input.GetIntInRange(logger, "minRange", 0, 0, 20);
		int maxRange = input.GetIntInRange(logger, "maxRange", this.minRange, this.minRange, 99);
		this.stepRange = input.GetIntInRange(logger, "stepRange", 1, 1, 99);

		this.rangeCount = MathUtil.CeiledDiv(maxRange - this.minRange + 1, this.stepRange);
		this.toolModeCount = this.rangeCount * (this.hasUpgradeMode ? 2 : 1);
		if(api is not ICoreClientAPI capi)
			return;

		this.toolModes = new SkillItem[this.toolModeCount];
		List<SkillItem?> toolModes = ObjectCacheUtil.GetOrCreate(api, "mapper:paintbrushToolModes", () => new List<SkillItem?>());
		toolModes.Resize((maxRange + 1) * 2);
		int refreshModeOffset = (this.hasUpgradeMode ? this.rangeCount : 0) - this.minRange;
		for(int i = this.minRange; i <= maxRange; i += this.stepRange) {
			int affectedCount = (i * 2 + 1) * (i * 2 + 1);
			if(this.hasUpgradeMode)
				this.toolModes[i / this.stepRange - this.minRange] = toolModes.GetOrCreateWithNumber(capi, i * 2, "mapper:toolmode-paintbrush-upgrade", i, affectedCount, "mapper:textures/icons/toolmode/paintbrush-upgrade.svg");
			this.toolModes[i / this.stepRange + refreshModeOffset] = toolModes.GetOrCreateWithNumber(capi, i * 2 + 1, "mapper:toolmode-paintbrush-refresh", i, affectedCount, "mapper:textures/icons/toolmode/paintbrush-refresh.svg");
		}
	}

	public override void OnUnloaded(ICoreAPI api) {
		base.OnUnloaded(api);
		if(this.toolModes != null)
			foreach(SkillItem toolMode in this.toolModes)
				toolMode.Dispose();
	}

	public override void SetToolMode(ItemSlot slot, IPlayer player, BlockSelection selection, int toolMode) {
		slot.Itemstack.Attributes.SetInt("toolMode", toolMode);
	}

	public override int GetToolMode(ItemSlot slot, IPlayer player, BlockSelection selection) {
		return Math.Min(this.toolModeCount - 1, slot.Itemstack.Attributes.GetInt("toolMode"));
	}

	public override SkillItem[]? GetToolModes(ItemSlot slot, IClientPlayer player, BlockSelection selection) {
		return this.toolModes;
	}

	public override WorldInteraction[] GetHeldInteractionHelp(ItemSlot slot) {
		return new WorldInteraction[] {
			new() {ActionLangCode = "Change tool mode", HotKeyCode = "toolmodeselect", MouseButton = EnumMouseButton.None},
		}.Append(base.GetHeldInteractionHelp(slot));
	}
}
