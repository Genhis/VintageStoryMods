namespace Mapper.GameContent;

using Mapper.Util;
using Mapper.WorldMap;
using System;
using System.Collections.Generic;
using System.Text;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.Server;
using Vintagestory.API.Util;

public class ItemMap : Item {
	private readonly ItemInteractionData interactionData = new();
	private SkillItem[]? toolModes;
	private int chunkCount;
	private int toolModeCount;

	public readonly struct CustomAttributes(int availablePixels, byte colorLevel, byte minZoomLevel, byte maxZoomLevel) {
		public readonly int AvailablePixels = availablePixels;
		public readonly byte ColorLevel = colorLevel;
		public readonly byte MinZoomLevel = minZoomLevel;
		public readonly byte MaxZoomLevel = maxZoomLevel;

		public CustomAttributes() : this(0, 0, 0, 0) {}
		public CustomAttributes(ITreeAttribute input) : this(input.GetInt("availablePixels"), (byte)input.GetInt("colorLevel"), (byte)input.GetInt("minZoomLevel"), (byte)input.GetInt("maxZoomLevel")) {}

		public readonly void Save(ITreeAttribute output) {
			output.SetInt("availablePixels", this.AvailablePixels);
			output.SetInt("colorLevel", this.ColorLevel);
			output.SetInt("minZoomLevel", this.MinZoomLevel);
			output.SetInt("maxZoomLevel", this.MaxZoomLevel);
		}

		public readonly bool CanMergeWith(in CustomAttributes other) => this.ColorLevel == other.ColorLevel && this.MinZoomLevel == other.MinZoomLevel && this.MaxZoomLevel == other.MaxZoomLevel;
		public readonly CustomAttributes WithAvailablePixels(int availablePixels) => new(availablePixels, this.ColorLevel, this.MinZoomLevel, this.MaxZoomLevel);
	}
	public CustomAttributes MapAttributes { get; private set; }

	public override void OnLoaded(ICoreAPI api) {
		base.OnLoaded(api);

		JsonObject input = this.GetMapperAttributes();
		ILogger logger = api.Logger;
		this.interactionData.OnLoaded(this, input["interactionData"]);
		this.chunkCount = input.GetIntInRange(logger, "mapChunkCount", 0, 0, 1 << 16);
		byte colorLevel = ItemPaintset.GetColorLevel(input, logger);
		byte minZoomLevel = (byte)input.GetIntInRange(logger, "minZoomLevel", 1, 1, 6);
		byte maxZoomLevel = (byte)(input.GetIntInRange(logger, "maxZoomLevel", minZoomLevel, minZoomLevel, 6) - 1);
		--minZoomLevel;

		this.toolModeCount = maxZoomLevel - minZoomLevel + 1;
		this.MapAttributes = new CustomAttributes(MapChunk.GetAvailablePixels(this.chunkCount, minZoomLevel), colorLevel, minZoomLevel, maxZoomLevel);
		if(api is not ICoreClientAPI capi)
			return;

		this.toolModes = new SkillItem[this.toolModeCount];
		List<SkillItem?> toolModes = ObjectCacheUtil.GetOrCreate(api, "mapper::mapToolModes", () => new List<SkillItem?>());
		toolModes.ResizeIfSmaller(maxZoomLevel + 1);
		for(byte i = minZoomLevel; i <= maxZoomLevel; ++i)
			this.toolModes[i - minZoomLevel] = toolModes.GetOrCreateWithNumber(capi, i, "mapper:toolmode-map-scale", 1 << i, 1 << (i * 2), $"mapper:textures/icons/toolmode/map-zoom-{Math.Min(i + 1, 5)}.svg");
	}

	public override void OnUnloaded(ICoreAPI api) {
		base.OnUnloaded(api);
		if(this.toolModes != null)
			foreach(SkillItem toolMode in this.toolModes)
				toolMode.Dispose();
	}

	public override void OnHeldInteractStart(ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, bool firstEvent, ref EnumHandHandling handling) {
		if(byEntity.Controls.ShiftKey) {
			base.OnHeldInteractStart(slot, byEntity, blockSel, entitySel, firstEvent, ref handling);
			return;
		}
		if(!firstEvent || this.api.Side == EnumAppSide.Client && !MapperChunkMapLayer.GetInstance(this.api).CheckEnabledClient())
			return;

		handling = EnumHandHandling.PreventDefault;
		this.interactionData.OnHeldInteractStart(slot, byEntity);
	}

	public override bool OnHeldInteractStep(float secondsUsed, ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel) {
		if(byEntity.Controls.ShiftKey)
			return base.OnHeldInteractStep(secondsUsed, slot, byEntity, blockSel, entitySel);
		return this.interactionData.OnHeldInteractStep(slot, byEntity, secondsUsed);
	}

	public override void OnHeldInteractStop(float secondsUsed, ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel) {
		if(byEntity.Controls.ShiftKey) {
			base.OnHeldInteractStop(secondsUsed, slot, byEntity, blockSel, entitySel);
			return;
		}
		if(!this.interactionData.OnHeldInteractStop(byEntity, secondsUsed))
			return;
		if(byEntity is not EntityPlayer{Player: IServerPlayer player})
			return;

		CustomAttributes attributes = this.MapAttributes;
		if(MapperChunkMapLayer.GetInstance(this.api).MarkChunksForRedraw(player, byEntity.Pos.ToChunkPosition(), int.MaxValue, attributes.AvailablePixels, attributes.ColorLevel, (byte)(this.GetToolMode(slot, player, blockSel) + attributes.MinZoomLevel)) != attributes.AvailablePixels)
			slot.TakeOutAndMarkDirty(1);
	}

	public override void SetToolMode(ItemSlot slot, IPlayer player, BlockSelection selection, int toolMode) {
		slot.Itemstack!.Attributes.SetInt("toolMode", toolMode);
	}

	public override int GetToolMode(ItemSlot slot, IPlayer player, BlockSelection selection) {
		return Math.Min(this.toolModeCount - 1, slot.Itemstack!.Attributes.GetInt("toolMode"));
	}

	public override SkillItem[]? GetToolModes(ItemSlot slot, IClientPlayer player, BlockSelection selection) {
		return this.toolModes;
	}

	public override void GetHeldItemInfo(ItemSlot slot, StringBuilder description, IWorldAccessor world, bool withDebugInfo) {
		base.GetHeldItemInfo(slot, description, world, withDebugInfo);

		StringBuilder chunkCount = new();
		StringBuilder scales = new();
		CustomAttributes attributes = this.MapAttributes;
		for(byte i = attributes.MinZoomLevel; i <= attributes.MaxZoomLevel; ++i) {
			chunkCount.Append(attributes.AvailablePixels / MapChunk.GetRequiredDurability(i)).Append(", ");
			scales.Append("1:").Append(1 << i).Append(", ");
		}

		description.AppendLine();
		description.AppendLine(Lang.Get("mapper:iteminfo-map-scales", scales.Remove(scales.Length - 2, 2)));
		description.AppendLine(Lang.Get("mapper:iteminfo-map-chunk-coverage", chunkCount.Remove(chunkCount.Length - 2, 2)));
	}

	public override WorldInteraction[] GetHeldInteractionHelp(ItemSlot slot) {
		return new WorldInteraction[] {
			new() {ActionLangCode = "mapper:heldhelp-use", MouseButton = EnumMouseButton.Right},
			new() {ActionLangCode = "Change tool mode", HotKeyCode = "toolmodeselect", MouseButton = EnumMouseButton.None},
		}.Append(base.GetHeldInteractionHelp(slot));
	}
}
