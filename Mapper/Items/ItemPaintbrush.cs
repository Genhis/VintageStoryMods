namespace Mapper.Items;

using Mapper.Util;
using Mapper.WorldMap;
using System;
using System.Collections.Generic;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.Server;
using Vintagestory.API.Util;

public class ItemPaintbrush : Item {
	private readonly ItemInteractionData interactionData = new();
	private SkillItem[]? toolModes;
	private byte colorLevel;
	private bool hasUpgradeMode;
	private int minRange;
	private int stepRange;
	private int rangeCount;
	private int toolModeCount;

	public override void OnLoaded(ICoreAPI api) {
		base.OnLoaded(api);

		JsonObject input = this.GetMapperAttributes();
		ILogger logger = api.Logger;
		this.interactionData.OnLoaded(this, input["interactionData"]);
		this.colorLevel = (byte)input.GetIntInRange(logger, "colorLevel", 0, 0, 3);
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

	public override void OnHeldInteractStart(ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, bool firstEvent, ref EnumHandHandling handling) {
		if(this.GetColorLevel(slot, byEntity).Slot == null) {
			if(!byEntity.Controls.ShiftKey && this.api is ICoreClientAPI capi)
				capi.TriggerIngameError(this, "no-paintset", Lang.Get("mapper:error-no-paintset"));
			base.OnHeldInteractStart(slot, byEntity, blockSel, entitySel, firstEvent, ref handling);
			return;
		}
		if(!firstEvent || this.api.Side == EnumAppSide.Client && !MapperChunkMapLayer.GetInstance(this.api).CheckEnabledClient())
			return;

		handling = EnumHandHandling.PreventDefault;
		this.interactionData.OnHeldInteractStart(slot, byEntity);
	}

	public override bool OnHeldInteractStep(float secondsUsed, ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel) {
		if(this.GetColorLevel(slot, byEntity).Slot == null)
			return base.OnHeldInteractStep(secondsUsed, slot, byEntity, blockSel, entitySel);
		return this.interactionData.OnHeldInteractStep(slot, byEntity, secondsUsed);
	}

	public override void OnHeldInteractStop(float secondsUsed, ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel) {
		ItemSlotAndColorLevel slotAndColor = this.GetColorLevel(slot, byEntity);
		if(slotAndColor.Slot == null) {
			base.OnHeldInteractStop(secondsUsed, slot, byEntity, blockSel, entitySel);
			return;
		}
		if(!this.interactionData.OnHeldInteractStop(byEntity, secondsUsed))
			return;
		if(byEntity is not EntityPlayer{Player: IServerPlayer player})
			return;

		ItemStack paintsetStack = slotAndColor.Slot.Itemstack;
		int oldDurability = paintsetStack.Collectible.GetRemainingDurability(paintsetStack);
		int mode = this.GetToolMode(slot, player, blockSel);
		int newDurability = MapperChunkMapLayer.GetInstance(this.api).MarkChunksForRedraw(player, byEntity.Pos.ToChunkPosition(), mode % this.rangeCount * this.stepRange + this.minRange, oldDurability * MapChunk.Area + paintsetStack.Attributes.GetInt("fractionalDurability"), slotAndColor.ColorLevel, ColorAndZoom.EmptyZoomLevel, !this.hasUpgradeMode || mode >= this.rangeCount);

		paintsetStack.Attributes.SetInt("fractionalDurability", newDurability % MapChunk.Area);
		paintsetStack.Collectible.DamageItem(byEntity.World, byEntity, slotAndColor.Slot, oldDurability - newDurability / MapChunk.Area);
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
			new() {ActionLangCode = "mapper:heldhelp-paint-area", MouseButton = EnumMouseButton.Right},
			new() {ActionLangCode = "Change tool mode", HotKeyCode = "toolmodeselect", MouseButton = EnumMouseButton.None},
		}.Append(base.GetHeldInteractionHelp(slot));
	}

	private struct ItemSlotAndColorLevel(ItemSlot? slot, byte colorLevel) {
		public ItemSlot? Slot = slot;
		public byte ColorLevel = colorLevel;
	}

	private ItemSlotAndColorLevel GetColorLevel(ItemSlot slot, EntityAgent entity) {
		if(entity.Controls.ShiftKey)
			return new ItemSlotAndColorLevel(null, 0);
		if(this.colorLevel > 0)
			return new ItemSlotAndColorLevel(slot, this.colorLevel);

		slot = entity.LeftHandItemSlot;
		byte colorLevel = (byte)(slot.Itemstack?.ItemAttributes?["mapper"]["colorLevel"].AsInt(0) ?? 0);
		if(colorLevel > 0)
			return new ItemSlotAndColorLevel(slot, colorLevel);
		return new ItemSlotAndColorLevel(null, 0);
	}
}
