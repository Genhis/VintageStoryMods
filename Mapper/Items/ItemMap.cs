namespace Mapper.Items;

using Mapper.Util;
using System;
using System.Collections.Generic;
using System.Text;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.Util;

public class ItemMap : Item {
	private SkillItem[]? toolModes;
	private int chunkCount;
	private byte colorLevel;
	private int minZoomLevel;
	private int availablePixels;
	private int toolModeCount;

	public override void OnLoaded(ICoreAPI api) {
		base.OnLoaded(api);

		JsonObject input = this.GetMapperAttributes();
		ILogger logger = api.Logger;
		this.chunkCount = input.GetIntInRange(logger, "mapChunkCount", 0, 0, 1 << 16);
		this.colorLevel = (byte)input.GetIntInRange(logger, "colorLevel", 0, 0, 3);
		this.minZoomLevel = input.GetIntInRange(logger, "minZoomLevel", 1, 1, 6);
		int maxZoomLevel = input.GetIntInRange(logger, "maxZoomLevel", this.minZoomLevel, this.minZoomLevel, 6);

		--this.minZoomLevel;
		this.availablePixels = this.chunkCount * 1024 / (1 << (this.minZoomLevel * 2));
		this.toolModeCount = maxZoomLevel - this.minZoomLevel;
		if(api is not ICoreClientAPI capi)
			return;

		this.toolModes = new SkillItem[this.toolModeCount];
		List<SkillItem?> toolModes = ObjectCacheUtil.GetOrCreate(api, "mapper::mapToolModes", () => new List<SkillItem?>());
		toolModes.Resize(maxZoomLevel);
		for(int i = this.minZoomLevel; i < maxZoomLevel; ++i)
			this.toolModes[i - this.minZoomLevel] = toolModes.GetOrCreateWithNumber(capi, i, "mapper:toolmode-map-scale", 1 << i, 1 << (i * 2), $"mapper:textures/icons/toolmode/map-zoom-{Math.Min(i + 1, 5)}.svg");
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

	public override void GetHeldItemInfo(ItemSlot slot, StringBuilder description, IWorldAccessor world, bool withDebugInfo) {
		base.GetHeldItemInfo(slot, description, world, withDebugInfo);

		StringBuilder chunkCount = new();
		StringBuilder scales = new();
		int maxZoomLevel = this.minZoomLevel + this.toolModeCount;
		for(int i = this.minZoomLevel; i < maxZoomLevel; ++i) {
			chunkCount.Append(this.availablePixels / (1024 >> (i * 2))).Append(", ");
			scales.Append("1:").Append(1 << i).Append(", ");
		}

		description.AppendLine();
		description.AppendLine(Lang.Get("mapper:iteminfo-map-scales", scales.Remove(scales.Length - 2, 2)));
		description.AppendLine(Lang.Get("mapper:iteminfo-map-chunk-coverage", chunkCount.Remove(chunkCount.Length - 2, 2)));
	}

	public override WorldInteraction[] GetHeldInteractionHelp(ItemSlot slot) {
		return new WorldInteraction[] {
			new() {ActionLangCode = "Change tool mode", HotKeyCode = "toolmodeselect", MouseButton = EnumMouseButton.None},
		}.Append(base.GetHeldInteractionHelp(slot));
	}
}
