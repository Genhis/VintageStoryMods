namespace Mapper.GameContent;

using Mapper.Util;
using Mapper.Util.IO;
using Mapper.WorldMap;
using System;
using System.Collections.Generic;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;

public class BlockEntityCartographyTable : BlockEntityContainer {
	public readonly MapChunks Chunks = [];
	public readonly object SaveLock = new();
	internal bool expectUpdate;
	internal int lastUpdateID;
	private ItemMap.CustomAttributes mapAttributes;

	private readonly InventoryCartographyTable inventory = new();
	private GuiDialogBlockEntityCartographyTable? guiDialog;

	public override InventoryBase Inventory => this.inventory;
	public override string InventoryClassName => "cartographytable";

	public BlockEntityCartographyTable() {
		this.inventory.SlotModified += this.OnInventoryModified;
	}

	public override void FromTreeAttributes(ITreeAttribute tree, IWorldAccessor world) {
		base.FromTreeAttributes(tree, world);

		lock(this.SaveLock) {
			ITreeAttribute? mapAttributes = tree.GetTreeAttribute("mapAttributes");
			this.mapAttributes = mapAttributes == null ? new() : new(mapAttributes);

			this.expectUpdate = false;
			this.lastUpdateID = tree.GetInt("lastUpdateID");
			this.Chunks.Clear();
			byte[]? chunksData = tree.GetBytesLarge("chunks");
			try {
				if(chunksData != null)
					this.Chunks.FromBytes(chunksData, null!); // Cartography table doesn't store unexplored chunks, so background is not necessary.
			}
			catch(Exception ex) {
				world.Api.Logger.Error("[mapper] Failed to load cartography table data:\n" + ex.ToString());
				this.Chunks.Clear();
			}
		}
		this.guiDialog?.UpdateChunkCount(this.Chunks.Count);
	}

	public override void ToTreeAttributes(ITreeAttribute tree) {
		base.ToTreeAttributes(tree);

		if(this.mapAttributes.AvailablePixels > 0) {
			TreeAttribute mapAttributes = new();
			this.mapAttributes.Save(mapAttributes);
			tree["mapAttributes"] = mapAttributes;
		}

		System.Diagnostics.Debug.Assert(!tree.HasLargeAttribute("chunks"));
		tree.SetInt("lastUpdateID", this.lastUpdateID);
		try {
			byte[]? data = this.Chunks.ToBytes();
			if(data != null) {
				int numParts = tree.SetBytesLarge("chunks", data);
				this.Api.Logger.VerboseDebug($"[mapper] Cartography table at {this.Pos} saved {this.Chunks.Count} chunks taking {data.Length / 1024.0 / 1024.0:0.###} MB of space split to {numParts} attributes");
			}
		}
		catch(Exception ex) {
			this.Api.Logger.Error("[mapper] Failed to save cartography table data:\n" + ex.ToString());
		}
	}

	private void Dispose() {
		if(this.guiDialog != null) {
			this.guiDialog.TryClose();
			System.Diagnostics.Debug.Assert(this.guiDialog == null);
		}
		if(this.Api.Side == EnumAppSide.Server)
			this.inventory.openedByPlayerGUIds.Clear();
	}

	public override void OnBlockUnloaded() {
		base.OnBlockUnloaded();
		this.Dispose();
	}

	public override void OnBlockRemoved() {
		base.OnBlockRemoved();
		this.Dispose();
	}

	private void OnInventoryModified(int slotID) {
		this.MarkDirty(this.Api.Side == EnumAppSide.Server);
	}

	public override bool OnTesselation(ITerrainMeshPool chunkMeshes, ITesselatorAPI tesselator) {
		// OnTesselation runs in a thread, so be extra careful when accessing the inventory.
		// I assume slots won't change, so the iterator should stay valid, but item stacks might.
		// Let's copy stack data to local variables and check if it can be used.
		float[] matrix = MathUtil.HorizontalBlockRotationMatrix[this.Block.LastCodePart()];
		foreach(ItemSlot slot in this.inventory) {
			ItemStack? stack = slot.Itemstack;
			if(stack == null)
				continue;

			CollectibleObject? obj = stack.Collectible;
			int stackSize = stack.StackSize;
			if(obj == null || stackSize <= 0)
				continue;

			BehaviorCartographyTableDisplay? displayBehavior = obj.GetBehavior<BehaviorCartographyTableDisplay>();
			if(displayBehavior != null && displayBehavior.Valid)
				chunkMeshes.AddMeshData(displayBehavior.GetMesh(stackSize, obj.MaxStackSize), matrix);
		}
		return false;
	}

	public override void OnReceivedClientPacket(IPlayer player, int packetID, byte[] data) {
		if(packetID == (int)EnumBlockEntityPacketId.Close) {
			player.InventoryManager.CloseInventory(this.inventory);
			return;
		}

		if(!this.Api.World.Claims.TryAccess(player, this.Pos, EnumBlockAccessFlags.Use)) {
			this.Api.World.Logger.Audit($"Player {player.PlayerName} sent a packet for a cartography table at {this.Pos} but has no claim access, rejected");
			return;
		}

		if(packetID == (int)EnumBlockEntityPacketId.Open)
			player.InventoryManager.OpenInventory(this.inventory);
		else if(packetID < (int)EnumBlockEntityPacketId.Open) {
			this.inventory.InvNetworkUtil.HandleClientPacket(player, packetID, data);
			this.Api.World.BlockAccessor.GetChunkAtBlockPos(this.Pos).MarkModified();
		}
	}

	public override void OnReceivedServerPacket(int packetID, byte[] data) {
		if(packetID == (int)EnumBlockEntityPacketId.Close) {
			((ICoreClientAPI)this.Api).World.Player.InventoryManager.CloseInventory(this.inventory);
			this.guiDialog?.TryClose();
		}
	}

	public override void DropContents(Vec3d position) {
		this.inventory.DropAll(position);
	}

	public readonly struct ProcessChunksResult(AssetLocation? usedMapCode, AssetLocation? usedPaintsetCode, int usedMapDurability, int usedPaintsetDurability) {
		public readonly AssetLocation? UsedMapCode = usedMapCode;
		public readonly AssetLocation? UsedPaintsetCode = usedPaintsetCode;
		public readonly int UsedMapDurability = usedMapDurability;
		public readonly int UsedPaintsetDurability = usedPaintsetDurability;
	}
	public ProcessChunksResult ProcessChunks(Dictionary<FastVec2i, ColorAndZoom> chunks, System.Func<FastVec2i, byte> getCurrentZoomLevel, Action<FastVec2i, ColorAndZoom>? processChunk, Action<FastVec2i>? ignoreChunk) {
		ItemStack? mapStack = this.inventory.MapSlot.Itemstack;
		ItemMap? mapItem = mapStack?.Item as ItemMap;
		ItemMap.CustomAttributes mapAttributes = mapItem != null ? mapItem.MapAttributes : this.mapAttributes.WithAvailablePixels(0);
		int mapStackPixels = mapItem != null ? mapAttributes.AvailablePixels * mapStack!.StackSize : 0;
		int mapLeftoverPixels = mapAttributes.CanMergeWith(this.mapAttributes) ? this.mapAttributes.AvailablePixels : 0;
		int mapTotalPixels = mapStackPixels + mapLeftoverPixels;
		int usedMapDurability = 0;

		ItemStack? paintsetStack = this.inventory.PainsetSlot.Itemstack;
		byte paintsetColorLevel = ItemPaintset.GetColorLevel(paintsetStack);
		int paintsetTotalPixels = ItemPaintset.GetAvailablePixels(paintsetStack);
		int usedPaintsetDurability = 0;
		foreach(KeyValuePair<FastVec2i, ColorAndZoom> item in chunks) {
			byte color = item.Value.Color;
			byte zoomLevel = item.Value.ZoomLevel;

			bool useMap = zoomLevel != getCurrentZoomLevel(item.Key);
			bool usePaintset = !useMap || color > mapAttributes.ColorLevel;
			if(useMap && (zoomLevel < mapAttributes.MinZoomLevel || zoomLevel > mapAttributes.MaxZoomLevel || mapTotalPixels <= usedMapDurability) ||
			 usePaintset && (color > paintsetColorLevel || paintsetTotalPixels <= usedPaintsetDurability)) {
				ignoreChunk?.Invoke(item.Key);
				continue;
			}
			System.Diagnostics.Debug.Assert(useMap | usePaintset);

			int usedDurability = MapChunk.GetRequiredDurability(zoomLevel);
			if(useMap)
				usedMapDurability += usedDurability;
			if(usePaintset)
				usedPaintsetDurability += usedDurability;

			processChunk?.Invoke(item.Key, item.Value);
		}

		ProcessChunksResult result = new(mapItem?.Code, paintsetStack?.Collectible.Code, usedMapDurability, usedPaintsetDurability);
		if(this.Api.Side == EnumAppSide.Client)
			return result; // Client doesn't really use any items; return what items would be used if this were the server.

		if(usedMapDurability > 0)
			this.ConsumeMap(mapAttributes, usedMapDurability, mapLeftoverPixels, mapTotalPixels);
		if(usedPaintsetDurability > 0)
			ItemPaintset.DamageItem(this.Api.World, null, this.inventory.PainsetSlot, paintsetTotalPixels, paintsetTotalPixels - usedPaintsetDurability);
		return result;
	}

	private void ConsumeMap(in ItemMap.CustomAttributes mapAttributes, int usedMapDurability, int mapLeftoverPixels, int mapTotalPixels) {
		if(usedMapDurability <= mapLeftoverPixels)
			this.mapAttributes = mapAttributes.WithAvailablePixels(mapLeftoverPixels - usedMapDurability);
		else if(usedMapDurability < mapTotalPixels) {
			System.Diagnostics.Debug.Assert(mapAttributes.AvailablePixels > 0);
			this.mapAttributes = mapAttributes.WithAvailablePixels((mapTotalPixels - usedMapDurability) % mapAttributes.AvailablePixels);
			this.inventory.MapSlot.TakeOutAndMarkDirty(MathUtil.CeiledDiv(usedMapDurability - mapLeftoverPixels, mapAttributes.AvailablePixels));
		}
		else {
			this.mapAttributes = new();
			this.inventory.MapSlot.TakeOutWholeAndMarkDirty();
		}
		this.MarkDirty();
	}

	public void ToggleGui(IPlayer player) {
		ICoreClientAPI capi = (ICoreClientAPI)this.Api;
		if(this.guiDialog == null) {
			this.guiDialog = new GuiDialogBlockEntityCartographyTable(this.Pos, this.inventory, capi);
			this.guiDialog.OnClosed += () => {
				this.guiDialog.Dispose();
				this.guiDialog = null;
			};
			this.guiDialog.UpdateChunkCount(this.Chunks.Count);
			if(!this.guiDialog.TryOpen())
				throw new InvalidOperationException("Cartography table GUI couldn't be opened");
			capi.Network.SendPacketClient(this.inventory.Open(player));
			capi.Network.SendBlockEntityPacket(this.Pos, (int)EnumBlockEntityPacketId.Open);
		}
		else
			this.guiDialog.TryClose();
	}
}
