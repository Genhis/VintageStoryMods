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

	private static void MergeRegions(Dictionary<RegionPosition, MapRegion> sourceRegions, Dictionary<RegionPosition, MapRegion> targetRegions) {
		foreach((RegionPosition regionPos, MapRegion sourceRegion) in sourceRegions) {
			if(!targetRegions.TryGetValue(regionPos, out MapRegion targetRegion)) {
				targetRegion = new MapRegion();
				targetRegions[regionPos] = targetRegion;
			}

			targetRegion.MergeFrom(sourceRegion, regionPos);
		}
	}

	public (byte[]?, int) SynchronizeMap(byte[] playerPixelData, ServerPlayerMap playerServerMap, MapBackground background, ref bool dirtyFlag) {
		// Update regions
		BlockEntityCartographersTable.MergeRegions(playerServerMap.Regions, this.regions);
		BlockEntityCartographersTable.MergeRegions(this.regions, playerServerMap.Regions);

		// Load data
		using ClientMapStorage incomingData = new();
		using ClientMapStorage existingData = new();
		incomingData.Load(VersionedReader.Create(new MemoryStream(playerPixelData, false), compressed: true), background);
		existingData.Load(VersionedReader.Create(new MemoryStream(((ICoreServerAPI)this.Api).WorldManager.SaveGame.GetData(this.StorageKey))), background);

		// Merge data and save result
		int updatedChunks = existingData.MergeSharedData(incomingData, background);
		MemoryStream tableData = new();
		existingData.Save(VersionedWriter.Create(tableData, compressed: true), ref dirtyFlag);

		if(updatedChunks > 0) {
			// Only save data if we actually updated table
			((ICoreServerAPI)this.Api).WorldManager.SaveGame.StoreData(this.StorageKey, tableData.ToArray());
			this.MarkDirty(true);
		}
		// But always return existing table data
		// since we can have new data the user needs
		return (tableData.ToArray(), updatedChunks);
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