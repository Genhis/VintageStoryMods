namespace Mapper.Blocks;
using Mapper.Util.IO;
using Mapper.WorldMap;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.GameContent;

/// <summary>
/// Block entity for the Cartographer's Table.
/// 
/// Data storage:
/// - TreeAttributes: "regions", "waypoints", "chunks" (split into parts)
/// - Each byte array must stay under 32KB due to VS TreeAttribute limitation
/// </summary>
public class BlockEntityCartographersTable : BlockEntity {
	private const string RegionsKey = "cartographersTableRegionsDataKey";
	private const string WaypointsKey = "cartographersTableWaypointsDataKey";
	private const string ChunksKey = "cartographersTableChunksDataKey";
	private const int MaxBytesPerAttribute = 30000; // Stay safely under 32KB limit

	private readonly Dictionary<RegionPosition, MapRegion> Regions = [];
	public readonly Dictionary<string, Waypoint> Waypoints = [];
	private readonly Dictionary<FastVec2i, MapChunk> Chunks = [];

	private static void MergeRegions(Dictionary<RegionPosition, MapRegion> sourceRegions, Dictionary<RegionPosition, MapRegion> targetRegions) {
		foreach((RegionPosition regionPos, MapRegion sourceRegion) in sourceRegions) {
			if(!targetRegions.TryGetValue(regionPos, out MapRegion targetRegion)) {
				targetRegion = new MapRegion();
				targetRegions[regionPos] = targetRegion;
			}

			targetRegion.MergeFrom(sourceRegion, regionPos);
		}
	}

	public static string BytesToString(long byteCount) {
		string[] suf = { "B", "KB", "MB", "GB", "TB", "PB", "EB" }; //Longs run out around EB
		if(byteCount == 0)
			return "0" + suf[0];
		long bytes = Math.Abs(byteCount);
		int place = Convert.ToInt32(Math.Floor(Math.Log(bytes, 1024)));
		double num = Math.Round(bytes / Math.Pow(1024, place), 1);
		return (Math.Sign(byteCount) * num).ToString() + suf[place];
	}

	public int MergeChunks(Dictionary<FastVec2i, MapChunk> incoming) {
		int updatedChunks = 0;
		foreach((FastVec2i chunkPosition, MapChunk incomingChunk) in incoming) {
			if(!(this.Chunks.TryGetValue(chunkPosition, out MapChunk existingChunk)) || incomingChunk > existingChunk) {
				this.Chunks[chunkPosition] = incomingChunk;
				updatedChunks++;
			}
		}

		return updatedChunks;
	}

	public (Dictionary<FastVec2i, MapChunk>, int, int) SynchronizeMap(ServerPlayerMap playerServerMap, Dictionary<FastVec2i, MapChunk> incomingChunks, List<Waypoint> playerWaypoints) {
		// Update regions
		MergeRegions(playerServerMap.Regions, this.Regions);
		// We only store explored regions
		// A user must explore a region to share the map with others
		Dictionary<RegionPosition, MapRegion> exploredRegions = playerServerMap.Regions.Where(r => incomingChunks.ContainsKey(new FastVec2i(r.Key.X, r.Key.Y))).ToDictionary();
		MergeRegions(this.Regions, exploredRegions);

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

		// Merge chunk data
		int updatedChunks = MergeChunks(incomingChunks);

		// Build chunks to send back
		Dictionary<FastVec2i, MapChunk> outgoingChunks = [];
		foreach((FastVec2i pos, MapChunk existingChunk) in this.Chunks) {
			if(!(incomingChunks.TryGetValue(pos, out MapChunk incomingChunk)) || existingChunk > incomingChunk) {
				outgoingChunks[pos] = existingChunk;
			}
		}

		// Return table data for the player to download
		return (outgoingChunks, updatedChunks, updatedWaypoints);
	}

	public override void ToTreeAttributes(ITreeAttribute tree) {
		base.ToTreeAttributes(tree);

		// Save regions
		using(MemoryStream regionsStream = new()) {
			using(VersionedWriter output = VersionedWriter.Create(regionsStream, leaveOpen: true, compressed: true)) {
				output.Write(this.Regions.Count);
				foreach((RegionPosition pos, MapRegion region) in this.Regions) {
					pos.Save(output);
					region.Save(output);
				}
			}
			tree.SetBytes(RegionsKey, regionsStream.ToArray());
		}

		// Save waypoints
		using(MemoryStream waypointsStream = new()) {
			using(VersionedWriter output = VersionedWriter.Create(waypointsStream, leaveOpen: true, compressed: true)) {
				output.Write(this.Waypoints.Count);
				foreach(Waypoint waypoint in this.Waypoints.Values) {
					output.Write(waypoint);
				}
			}
			tree.SetBytes(WaypointsKey, waypointsStream.ToArray());
		}

		// Save chunks - split into multiple attributes to stay under 32KB limit
		if(this.Chunks.Count > 0) {
			// First, serialize all chunk data
			byte[] allChunksData;
			using(MemoryStream chunksStream = new()) {
				using(VersionedWriter output = VersionedWriter.Create(chunksStream, leaveOpen: true, compressed: true)) {
					output.Write(this.Chunks);
				}
				allChunksData = chunksStream.ToArray();
			}

			// Split into parts under 30KB each
			int numParts = (allChunksData.Length + MaxBytesPerAttribute - 1) / MaxBytesPerAttribute;
			tree.SetInt(ChunksKey + "_parts", numParts);
			tree.SetInt(ChunksKey + "_totalLen", allChunksData.Length);

			for(int i = 0; i < numParts; i++) {
				int offset = i * MaxBytesPerAttribute;
				int length = Math.Min(MaxBytesPerAttribute, allChunksData.Length - offset);
				byte[] part = new byte[length];
				Array.Copy(allChunksData, offset, part, 0, length);
				tree.SetBytes(ChunksKey + "_" + i, part);
			}

			this.Api.Logger.Notification($"[mapper] Cartographer's Table saving {this.Chunks.Count} chunks ({BytesToString(allChunksData.Length)}) in {numParts} parts");
		}
		else {
			tree.SetInt(ChunksKey + "_parts", 0);
			this.Api.Logger.Notification($"[mapper] Cartographer's Table saved nothing");
		}
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

		// Load chunks - reassemble from multiple attributes
		this.Chunks.Clear();
		int numParts = tree.GetInt(ChunksKey + "_parts", 0);
		if(numParts > 0) {
			try {
				int totalLen = tree.GetInt(ChunksKey + "_totalLen", 0);
				byte[] allChunksData = new byte[totalLen];
				int offset = 0;

				for(int i = 0; i < numParts; i++) {
					byte[]? part = tree.GetBytes(ChunksKey + "_" + i);
					if(part != null) {
						Array.Copy(part, 0, allChunksData, offset, part.Length);
						offset += part.Length;
					}
				}

				using VersionedReader input = VersionedReader.Create(new MemoryStream(allChunksData, false), compressed: true);
				input.ReadChunks(this.Chunks, null); // No background saved in Cartographer's table, just pure chunks
				this.Api?.Logger.Notification($"[mapper] Cartographer's Table loaded {this.Chunks.Count} chunks from {numParts} parts");
			}
			catch(Exception ex) {
				this.Api?.Logger.Warning($"[mapper] Failed to load Cartographer's Table chunks: {ex.Message}");
				this.Chunks.Clear();
			}
		}
	}
}
