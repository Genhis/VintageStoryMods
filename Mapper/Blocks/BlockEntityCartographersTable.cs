namespace Mapper.Blocks;
using Mapper.Util.IO;
using Mapper.WorldMap;
using System;
using System.Collections.Generic;
using System.IO;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.Server;
using Vintagestory.GameContent;

/// <summary>
/// Block entity for the Cartographer's Table.
/// 
/// Data storage:
/// - TreeAttributes: "regions", "waypoints"
/// - SaveGame.StoreData: pixel data
/// </summary>
public class BlockEntityCartographersTable : BlockEntity {
	private const string RegionsKey = "regions";
	private const string WaypointsKey = "waypoints";

	private readonly Dictionary<RegionPosition, MapRegion> Regions = [];
	public readonly Dictionary<string, Waypoint> Waypoints = [];
	private byte[]? PixelData;
	private bool pixelDataLoaded;

	/// <summary>
	/// Gets a unique storage key for this table's pixel data based on position.
	/// </summary>
	private string PixelDataStorageKey => $"mapper:cartographers-table:{this.Pos.X}:{this.Pos.Y}:{this.Pos.Z}";

	private static void MergeRegions(Dictionary<RegionPosition, MapRegion> sourceRegions, Dictionary<RegionPosition, MapRegion> targetRegions) {
		foreach((RegionPosition regionPos, MapRegion sourceRegion) in sourceRegions) {
			if(!targetRegions.TryGetValue(regionPos, out MapRegion targetRegion)) {
				targetRegion = new MapRegion();
				targetRegions[regionPos] = targetRegion;
			}

			targetRegion.MergeFrom(sourceRegion, regionPos);
		}
	}

	/// <summary>
	/// Loads pixel data from SaveGame storage if not already loaded.
	/// </summary>
	private void EnsurePixelDataLoaded() {
		if(this.pixelDataLoaded || this.Api?.Side != EnumAppSide.Server)
			return;

		this.pixelDataLoaded = true;
		this.PixelData = ((ICoreServerAPI)this.Api).WorldManager.SaveGame.GetData(this.PixelDataStorageKey);
	}

	private static string BytesToString(long byteCount) {
		string[] suf = { "B", "KB", "MB", "GB", "TB", "PB", "EB" }; //Longs run out around EB
		if(byteCount == 0)
			return "0" + suf[0];
		long bytes = Math.Abs(byteCount);
		int place = Convert.ToInt32(Math.Floor(Math.Log(bytes, 1024)));
		double num = Math.Round(bytes / Math.Pow(1024, place), 1);
		return (Math.Sign(byteCount) * num).ToString() + suf[place];
	}

	/// <summary>
	/// Saves pixel data to SaveGame storage.
	/// </summary>
	private void SavePixelData() {
		if(this.Api?.Side != EnumAppSide.Server)
			return;

		if(this.PixelData != null) {
			this.Api.Logger.Notification($"[mapper] Saving {BytesToString(this.PixelData.Length)} of PixelData");
			((ICoreServerAPI)this.Api).WorldManager.SaveGame.StoreData(this.PixelDataStorageKey, this.PixelData);
		}
	}

	public (byte[]?, int, int) SynchronizeMap(byte[] playerPixelData, ServerPlayerMap playerServerMap, List<Waypoint> playerWaypoints, MapBackground? background, ref bool dirtyFlag) {
		// Update regions
		MergeRegions(playerServerMap.Regions, this.Regions);
		MergeRegions(this.Regions, playerServerMap.Regions);

		// Update waypoints: player waypoints overwrite table waypoints
		int updatedWaypoints = 0;
		foreach(Waypoint playerWaypoint in playerWaypoints) {
			if(string.IsNullOrEmpty(playerWaypoint.Guid))
				continue;

			if(!this.Waypoints.TryGetValue(playerWaypoint.Guid, out Waypoint? tableWaypoint) || playerWaypoint != tableWaypoint) {
				this.Waypoints[playerWaypoint.Guid] = playerWaypoint;
				updatedWaypoints++;
			}
		}

		// Ensure pixel data is loaded before merging
		this.EnsurePixelDataLoaded();

		// Load pixel data
		using ClientMapStorage incomingData = new();
		using ClientMapStorage existingData = new();
		incomingData.Load(VersionedReader.Create(new MemoryStream(playerPixelData, false), compressed: true), background);

		if(this.PixelData != null) {
			try {
				existingData.Load(VersionedReader.Create(new MemoryStream(this.PixelData, false), compressed: true), background);
			}
			catch(Exception ex) {
				this.Api.Logger.Warning($"[mapper] Cartographer's Table data was corrupted, starting fresh: {ex.Message}");
				existingData.Chunks.Clear();
				existingData.ChunksToRedraw.Clear();
			}
		}

		// Merge pixel data and save result
		int updatedChunks = existingData.MergeSharedData(incomingData);
		MemoryStream tableData = new();
		using(VersionedWriter output = VersionedWriter.Create(tableData, leaveOpen: true, compressed: true)) {
			existingData.Save(output, ref dirtyFlag);
		}

		if(updatedChunks > 0) {
			this.PixelData = tableData.ToArray();
			this.SavePixelData();
			this.MarkDirty(false);
		}

		// Return table data for the player to download
		return (tableData.ToArray(), updatedChunks, updatedWaypoints);
	}

	public override void OnBlockRemoved() {
		base.OnBlockRemoved();

		// Clean up stored pixel data when table is removed (server-side only)
		if(this.Api?.Side == EnumAppSide.Server) {
			((ICoreServerAPI)this.Api).WorldManager.SaveGame.StoreData(this.PixelDataStorageKey, null);
		}
	}

	public override void ToTreeAttributes(ITreeAttribute tree) {
		base.ToTreeAttributes(tree);

		// Save regions (small metadata, OK for network sync)
		if(this.Regions.Count > 0) {
			using MemoryStream regionsStream = new();
			using(VersionedWriter output = VersionedWriter.Create(regionsStream, leaveOpen: true, compressed: true)) {
				output.Write(this.Regions.Count);
				foreach((RegionPosition pos, MapRegion region) in this.Regions) {
					pos.Save(output);
					region.Save(output);
				}
			}
			tree.SetBytes(RegionsKey, regionsStream.ToArray());
		}

		// Save waypoints (small metadata, OK for network sync)
		if(this.Waypoints.Count > 0) {
			using MemoryStream waypointsStream = new();
			using(VersionedWriter output = VersionedWriter.Create(waypointsStream, leaveOpen: true, compressed: true)) {
				output.Write(this.Waypoints.Count);
				foreach(Waypoint waypoint in this.Waypoints.Values) {
					output.Write(waypoint);
				}
			}
			tree.SetBytes(WaypointsKey, waypointsStream.ToArray());
		}

		// NOTE: PixelData is NOT stored in TreeAttributes because it's too large for network sync.
		// It's stored separately via SaveGame.StoreData in SavePixelData().
	}

	public override void FromTreeAttributes(ITreeAttribute tree, IWorldAccessor worldAccessForResolve) {
		base.FromTreeAttributes(tree, worldAccessForResolve);

		// Load regions
		this.Regions.Clear();
		byte[]? regionsData = tree.GetBytes(RegionsKey);
		if(regionsData != null && regionsData.Length > 0) {
			try {
				using VersionedReader input = VersionedReader.Create(new MemoryStream(regionsData, false), compressed: true);
				int count = input.ReadInt32();
				for(int i = 0; i < count; ++i) {
					this.Regions[new RegionPosition(input)] = new MapRegion(input);
				}
			}
			catch(Exception ex) {
				this.Api?.Logger.Warning($"[mapper] Failed to load Cartographer's Table regions: {ex.Message}");
				this.Regions.Clear();
			}
		}

		// Load waypoints
		this.Waypoints.Clear();
		byte[]? waypointsData = tree.GetBytes(WaypointsKey);
		if(waypointsData != null && waypointsData.Length > 0) {
			try {
				using VersionedReader input = VersionedReader.Create(new MemoryStream(waypointsData, false), compressed: true);
				int count = input.ReadInt32();
				for(int i = 0; i < count; ++i) {
					Waypoint waypoint = input.ReadWaypoint();
					if(!string.IsNullOrEmpty(waypoint.Guid))
						this.Waypoints[waypoint.Guid] = waypoint;
				}
			}
			catch(Exception ex) {
				this.Api?.Logger.Warning($"[mapper] Failed to load Cartographer's Table waypoints: {ex.Message}");
				this.Waypoints.Clear();
			}
		}

		// PixelData is loaded lazily from SaveGame storage when needed
		this.pixelDataLoaded = false;
	}
}