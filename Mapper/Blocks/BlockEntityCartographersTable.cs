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

	public (byte[]?, bool) SynchronizeMap(byte[] playerPixelData, ServerPlayerMap playerServerMap, MapBackground background) {
		BlockEntityCartographersTable.MergeRegions(playerServerMap.Regions, this.regions);
		BlockEntityCartographersTable.MergeRegions(this.regions, playerServerMap.Regions);

		if(playerPixelData != null) {
			(byte[]? mergedPixelData, bool tableWasUpdated) = this.MergeCompressedPixelData(playerPixelData, background);
			if(tableWasUpdated) {
				((ICoreServerAPI)this.Api).WorldManager.SaveGame.StoreData(this.StorageKey, mergedPixelData);
				this.MarkDirty(true);
			}
			return (mergedPixelData, tableWasUpdated);
		}
		return (null, false);
	}

	private (byte[], bool) MergeCompressedPixelData(byte[] incomingData, MapBackground background) {
		Dictionary<FastVec2i, MapChunk> chunks = [];
		bool hadChanges = false;

		byte[]? existingData = ((ICoreServerAPI)this.Api).WorldManager.SaveGame.GetData(this.StorageKey);
		if(existingData != null) {
			// Load existing table data
			try {
				using VersionedReader input = VersionedReader.Create(new MemoryStream(existingData, false), compressed: true);
				for(int i = 0; i < input.ReadInt32(); ++i) {
					FastVec2i pos = input.ReadFastVec2i();
					chunks[pos] = new(input, pos, background);
				}
			}
			catch {
				chunks.Clear();
			}
		}

		// Merge incoming player data, keeping better quality chunks
		try {
			using VersionedReader input = VersionedReader.Create(new MemoryStream(incomingData, false), compressed: true);
			int count = input.ReadInt32();
			for(int i = 0; i < count; ++i) {
				FastVec2i pos = input.ReadFastVec2i();
				MapChunk incoming = new(input, pos, background);

				// Update map if
				// 1. Map chunk doesn't exist
				// 2. Zoom level is lower/resolution is higher
				// 3. Zoom level is the same but color level is higher
				// This means that resolution takes precedence over color level.
				// i.e. higher resolution B/W maps replaces lower resolution colored maps
				// - this was an intentional design decision
				if(!chunks.TryGetValue(pos, out MapChunk existing) ||
					 incoming.ZoomLevel < existing.ZoomLevel ||
					 incoming.ZoomLevel == existing.ZoomLevel && incoming.ColorLevel > existing.ColorLevel
				) {
					chunks[pos] = incoming;
					hadChanges = true;
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
			foreach(KeyValuePair<FastVec2i, MapChunk> item in chunks) {
				output.Write(item.Key);
				item.Value.Save(output);
			}
		}
		return (outStream.ToArray(), hadChanges);
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