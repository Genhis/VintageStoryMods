namespace Mapper.Blocks;
using Mapper.Util.IO;
using Mapper.WorldMap;
using System;
using System.Collections.Generic;
using System.IO;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;

/// <summary>
/// Block entity for the Cartographer's Table.
/// 
/// Data is stored in two parts:
/// - Region metadata (zoom/color levels per chunk) - stored in TreeAttributes for block entity sync
/// - Pixel data - stored separately in world save to avoid size limits on TreeAttribute serialization
/// </summary>
public class BlockEntityCartographersTable : BlockEntity {
	private readonly Dictionary<RegionPosition, MapRegion> regions = [];
	private string StorageKey => $"mapper:table:{this.Pos.X}_{this.Pos.Y}_{this.Pos.Z}";

	public override void Initialize(ICoreAPI api) {
		base.Initialize(api);

		if(api is ICoreServerAPI sapi) {
			this.LoadPixelDataFromWorldSave(sapi);
		}
	}

	private byte[]? LoadPixelDataFromWorldSave(ICoreServerAPI sapi) {
		try {
			return sapi.WorldManager.SaveGame.GetData(this.StorageKey);
		}
		catch(Exception ex) {
			sapi.Logger.Error($"[mapper] Failed to load CartographersTable pixel data: {ex.Message}");
			return null;
		}
	}

	private void SavePixelDataToWorldSave(ICoreServerAPI sapi, byte[]? data) {
		try {
			if(data != null && data.Length > 4) {
				sapi.WorldManager.SaveGame.StoreData(this.StorageKey, data);
			}
		}
		catch(Exception ex) {
			sapi.Logger.Error($"[mapper] Failed to save CartographersTable pixel data: {ex.Message}");
		}
	}

	/// <summary>
	/// Merges the player's pixel data into the table (player -> table).
	/// </summary>
	/// <returns>The table's complete pixel data and whether any new data was added to the table.</returns>
	public (byte[]? tableData, bool tableWasUpdated) SynchronizeMap(IServerPlayer player, byte[] playerPixelData, ServerPlayerMap playerServerMap) {
		ICoreServerAPI sapi = (ICoreServerAPI)this.Api;
		this.MergeRegionsFrom(playerServerMap.Regions);
		Dictionary<FastVec2i, ColorAndZoom> changesForPlayer = this.MergeRegionsInto(playerServerMap.Regions);
		byte[]? currentPixelData = this.LoadPixelDataFromWorldSave(sapi);
		(byte[]? mergedPixelData, bool tableWasUpdated) = this.MergePixelData(currentPixelData, playerPixelData);
		if(tableWasUpdated) {
			this.SavePixelDataToWorldSave(sapi, mergedPixelData);
			this.MarkDirty(true);
		}
		return (mergedPixelData, tableWasUpdated);
	}

	/// <summary>
	/// Merges player's region metadata into the table (player -> table).
	/// </summary>
	private void MergeRegionsFrom(Dictionary<RegionPosition, MapRegion> sourceRegions) {
		foreach(KeyValuePair<RegionPosition, MapRegion> sourceItem in sourceRegions) {
			RegionPosition regionPos = sourceItem.Key;
			MapRegion sourceRegion = sourceItem.Value;

			if(!this.regions.TryGetValue(regionPos, out MapRegion targetRegion)) {
				targetRegion = new MapRegion();
				this.regions[regionPos] = targetRegion;
			}

			Dictionary<FastVec2i, ColorAndZoom> _ = [];
			targetRegion.MergeFrom(sourceRegion, _, regionPos);
		}
	}

	/// <summary>
	/// Merges table's region metadata into the player's map (table -> player).
	/// </summary>
	private Dictionary<FastVec2i, ColorAndZoom> MergeRegionsInto(Dictionary<RegionPosition, MapRegion> targetRegions) {
		Dictionary<FastVec2i, ColorAndZoom> changes = [];

		foreach(KeyValuePair<RegionPosition, MapRegion> sourceItem in this.regions) {
			RegionPosition regionPos = sourceItem.Key;
			MapRegion sourceRegion = sourceItem.Value;

			if(!targetRegions.TryGetValue(regionPos, out MapRegion targetRegion)) {
				targetRegion = new MapRegion();
				targetRegions[regionPos] = targetRegion;
			}

			targetRegion.MergeFrom(sourceRegion, changes, regionPos);
		}

		return changes;
	}

	private (byte[]? data, bool hadChanges) MergePixelData(byte[]? existingData, byte[] incomingData) {
		if(existingData == null) {
			return (incomingData, incomingData != null && incomingData.Length > 4);
		}

		if(incomingData == null) {
			return (existingData, false);
		}

		return MergeCompressedPixelData(existingData, incomingData);
	}

	private static (byte[] data, bool hadChanges) MergeCompressedPixelData(byte[] existingData, byte[] incomingData) {
		Dictionary<FastVec2i, (byte zoomLevel, byte colorLevel, int[]? pixels)> chunks = [];
		bool hadChanges = false;

		// Load existing table data
		try {
			using(MemoryStream stream = new(existingData, false))
			using(VersionedReader input = VersionedReader.Create(stream, compressed: true)) {
				int count = input.ReadInt32();
				for(int i = 0; i < count; ++i) {
					FastVec2i pos = input.ReadFastVec2i();
					byte data = input.ReadUInt8();
					byte colorLevel = (byte)(data >> ColorAndZoom.ZoomBits);
					byte zoomLevel = (byte)(data & ColorAndZoom.ZoomMask);
					int[]? pixels = colorLevel > 0 ? ReadPixels(input, zoomLevel) : null;
					chunks[pos] = (zoomLevel, colorLevel, pixels);
				}
			}
		}
		catch {
			chunks.Clear();
		}

		// Merge incoming player data, keeping better quality chunks
		try {
			using(MemoryStream stream = new(incomingData, false))
			using(VersionedReader input = VersionedReader.Create(stream, compressed: true)) {
				int count = input.ReadInt32();
				for(int i = 0; i < count; ++i) {
					FastVec2i pos = input.ReadFastVec2i();
					byte data = input.ReadUInt8();
					byte incomingColor = (byte)(data >> ColorAndZoom.ZoomBits);
					byte incomingZoom = (byte)(data & ColorAndZoom.ZoomMask);
					int[]? incomingPixels = incomingColor > 0 ? ReadPixels(input, incomingZoom) : null;


					// Update map if
					// 1. Map chunk doesn't exist
					// 2. Zoom level is lower/resolution is higher
					// 3. Zoom level is the same but color level is higher
					// This means that resolution takes precedence over color level.
					// i.e. higher resolution B/W maps replaces lower resolution colored maps
					// - this was an intentional design decision
					if(!chunks.TryGetValue(pos, out var existing) ||
					   incomingZoom < existing.zoomLevel ||
					   incomingZoom == existing.zoomLevel && incomingColor > existing.colorLevel
					) {
						chunks[pos] = (incomingZoom, incomingColor, incomingPixels);
						hadChanges = true;
					}
				}
			}
		}
		catch {
			// Ignore corrupted incoming data
		}

		// Write merged result
		using MemoryStream outStream = new();
		using(VersionedWriter output = VersionedWriter.Create(outStream, leaveOpen: true, compressed: true)) {
			output.Write(chunks.Count);
			foreach(KeyValuePair<FastVec2i, (byte zoomLevel, byte colorLevel, int[]? pixels)> item in chunks) {
				output.Write(item.Key);
				byte data = (byte)((item.Value.colorLevel << ColorAndZoom.ZoomBits) | (item.Value.zoomLevel & ColorAndZoom.ZoomMask));
				output.Write(data);
				if(item.Value.colorLevel > 0 && item.Value.pixels != null)
					WritePixels(output, item.Value.pixels, item.Value.zoomLevel);
			}
		}
		return (outStream.ToArray(), hadChanges);
	}

	private static int[] ReadPixels(VersionedReader input, byte zoomLevel) {
		int[] pixels = new int[MapChunk.Area];
		int scaleFactor = 1 << zoomLevel;
		for(int y = 0; y < MapChunk.Size; y += scaleFactor)
			for(int x = 0; x < MapChunk.Size; x += scaleFactor) {
				int pixel = input.ReadInt32();
				for(int innerY = 0; innerY < scaleFactor; ++innerY) {
					int rowOffset = (y + innerY) * MapChunk.Size + x;
					for(int innerX = 0; innerX < scaleFactor; ++innerX)
						pixels[rowOffset + innerX] = pixel;
				}
			}
		return pixels;
	}

	private static void WritePixels(VersionedWriter output, int[] pixels, byte zoomLevel) {
		int scaleFactor = 1 << zoomLevel;
		for(int y = 0; y < MapChunk.Size; y += scaleFactor) {
			int rowOffset = y * MapChunk.Size;
			for(int x = 0; x < MapChunk.Size; x += scaleFactor)
				output.Write(pixels[rowOffset + x]);
		}
	}

	public override void ToTreeAttributes(ITreeAttribute tree) {
		base.ToTreeAttributes(tree);
		try {
			using MemoryStream regionStream = new();
			using(VersionedWriter output = VersionedWriter.Create(regionStream, leaveOpen: true)) {
				output.Write(this.regions.Count);
				foreach(KeyValuePair<RegionPosition, MapRegion> item in this.regions) {
					item.Key.Save(output);
					item.Value.Save(output);
				}
			}
			tree.SetBytes("mapperRegions", regionStream.ToArray());
		}
		catch(Exception ex) {
			this.Api?.Logger.Error("[mapper] Failed to save CartographersTable regions to tree: " + ex.Message);
		}
	}

	public override void FromTreeAttributes(ITreeAttribute tree, IWorldAccessor worldAccessForResolve) {
		base.FromTreeAttributes(tree, worldAccessForResolve);

		byte[]? regionData = tree.GetBytes("mapperRegions");
		this.regions.Clear();
		if(regionData != null && regionData.Length > 0) {
			try {
				using MemoryStream stream = new(regionData, false);
				using VersionedReader input = VersionedReader.Create(stream);
				int count = input.ReadInt32();
				for(int i = 0; i < count; ++i) {
					RegionPosition pos = new(input);
					MapRegion region = new(input);
					this.regions[pos] = region;
				}
			}
			catch(Exception ex) {
				this.Api?.Logger.Error("[mapper] Failed to load CartographersTable regions from tree: " + ex.Message);
			}
		}
	}

	public override void OnBlockRemoved() {
		base.OnBlockRemoved();
		if(this.Api is ICoreServerAPI sapi) {
			try {
				sapi.WorldManager.SaveGame.StoreData(this.StorageKey, null);
			}
			catch {
				// Ignore cleanup errors
			}
		}
	}

	public int GetRegionCount() => this.regions.Count;
}