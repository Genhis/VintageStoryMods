namespace Mapper.Blocks;
using Mapper.Util.IO;
using Mapper.WorldMap;
using System;
using System.Collections.Generic;
using System.IO;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;

/// <summary>
/// Block entity for the Cartographer's Table.
/// 
/// All data is stored in TreeAttributes:
/// - "regions" - Region metadata (zoom/color levels per chunk)
/// - "pixelData" - Serialized pixel data for all map chunks
/// </summary>
public class BlockEntityCartographersTable : BlockEntity {
	private const string RegionsKey = "regions";
	private const string PixelDataKey = "pixelData";

	private readonly Dictionary<RegionPosition, MapRegion> regions = [];
	private byte[]? pixelData;

	private static void MergeRegions(Dictionary<RegionPosition, MapRegion> sourceRegions, Dictionary<RegionPosition, MapRegion> targetRegions) {
		foreach((RegionPosition regionPos, MapRegion sourceRegion) in sourceRegions) {
			if(!targetRegions.TryGetValue(regionPos, out MapRegion targetRegion)) {
				targetRegion = new MapRegion();
				targetRegions[regionPos] = targetRegion;
			}

			targetRegion.MergeFrom(sourceRegion, regionPos);
		}
	}

	public (byte[]?, int) SynchronizeMap(byte[] playerPixelData, ServerPlayerMap playerServerMap, MapBackground? background, ref bool dirtyFlag) {
		// Update regions
		BlockEntityCartographersTable.MergeRegions(playerServerMap.Regions, this.regions);
		BlockEntityCartographersTable.MergeRegions(this.regions, playerServerMap.Regions);

		// Load data
		using ClientMapStorage incomingData = new();
		using ClientMapStorage existingData = new();
		incomingData.Load(VersionedReader.Create(new MemoryStream(playerPixelData, false), compressed: true), background);

		if(this.pixelData != null) {
			try {
				existingData.Load(VersionedReader.Create(new MemoryStream(this.pixelData, false), compressed: true), background);
			}
			catch(Exception ex) {
				this.Api.Logger.Warning($"[mapper] Cartographer's Table data was corrupted, starting fresh: {ex.Message}");
				existingData.Chunks.Clear();
				existingData.ChunksToRedraw.Clear();
			}
		}

		// Merge data and save result
		int updatedChunks = existingData.MergeSharedData(incomingData);
		MemoryStream tableData = new();
		using(VersionedWriter output = VersionedWriter.Create(tableData, leaveOpen: true, compressed: true)) {
			existingData.Save(output, ref dirtyFlag);
		}

		if(updatedChunks > 0) {
			// Store the pixel data and mark dirty for save (but don't sync to clients - data is too large)
			this.pixelData = tableData.ToArray();
			this.MarkDirty(false);
		}
		// But always return existing table data
		// since we can have new data the user needs
		return (tableData.ToArray(), updatedChunks);
	}

	public override void ToTreeAttributes(ITreeAttribute tree) {
		base.ToTreeAttributes(tree);

		// Save regions
		if(this.regions.Count > 0) {
			using MemoryStream regionsStream = new();
			using(VersionedWriter output = VersionedWriter.Create(regionsStream, leaveOpen: true, compressed: true)) {
				output.Write(this.regions.Count);
				foreach(KeyValuePair<RegionPosition, MapRegion> item in this.regions) {
					item.Key.Save(output);
					item.Value.Save(output);
				}
			}
			tree.SetBytes(RegionsKey, regionsStream.ToArray());
		}

		// Save pixel data
		if(this.pixelData != null) {
			tree.SetBytes(PixelDataKey, this.pixelData);
		}
	}

	public override void FromTreeAttributes(ITreeAttribute tree, IWorldAccessor worldAccessForResolve) {
		base.FromTreeAttributes(tree, worldAccessForResolve);

		// Load regions
		this.regions.Clear();
		byte[]? regionsData = tree.GetBytes(RegionsKey);
		if(regionsData != null && regionsData.Length > 0) {
			try {
				using VersionedReader input = VersionedReader.Create(new MemoryStream(regionsData, false), compressed: true);
				int count = input.ReadInt32();
				for(int i = 0; i < count; ++i) {
					this.regions[new RegionPosition(input)] = new MapRegion(input);
				}
			}
			catch(Exception ex) {
				this.Api?.Logger.Warning($"[mapper] Failed to load Cartographer's Table regions: {ex.Message}");
				this.regions.Clear();
			}
		}

		// Load pixel data
		this.pixelData = tree.GetBytes(PixelDataKey);
	}

	public int GetRegionCount() => this.regions.Count;
}