namespace Mapper.WorldMap;

using Mapper.Util;
using Mapper.Util.Reflection;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.API.Util;
using Vintagestory.GameContent;

public class MapperChunkMapLayer : ChunkMapLayer {
	private static readonly FieldAccessor<ChunkMapLayer, UniqueQueue<FastVec2i>> chunksToGen = new("chunksToGen");
	private static readonly FieldAccessor<ChunkMapLayer, object> chunksToGenLock = new("chunksToGenLock");
	private static readonly FieldAccessor<ChunkMapLayer, ConcurrentQueue<ReadyMapPiece>> readyMapPieces = new("readyMapPieces");

	// Server variables
	private readonly ServerMapStorage? serverStorage;
	private readonly HashSet<string>? joiningPlayers;

	// Client variables
	private readonly ClientMapStorage? clientStorage;
	private readonly object? chunksToRedrawLock;
	private float lastThreadUpdateTime;

	public override EnumMapAppSide DataSide => EnumMapAppSide.Server;

	public MapperChunkMapLayer(ICoreAPI api, IWorldMapManager mapSink) : base(api, mapSink) {
		if(api is ICoreServerAPI sapi) {
			this.serverStorage = [];
			this.joiningPlayers = [];
			sapi.Event.PlayerJoin += player => this.joiningPlayers.Add(player.PlayerUID);
		}
		else {
			this.clientStorage = new ClientMapStorage();
			this.chunksToRedrawLock = new();
		}
	}

	public override void OnLoaded() {
		base.OnLoaded();

		MapperModSystem modSystem = this.api.ModLoader.GetModSystem<MapperModSystem>();
		if(modSystem.mapLayer != null)
			throw new InvalidOperationException("Another MapperChunkMapLayer instance is already loaded");
		modSystem.mapLayer = this;
	}

	public override void OnShutDown() {
		this.api.ModLoader.GetModSystem<MapperModSystem>().mapLayer = null;
		base.OnShutDown();
	}

	public int MarkChunksForRedraw(IServerPlayer player, FastVec2i chunkPosition, int radius, int durability, byte colorLevel, byte zoomLevel, bool forceOverdraw = false) {
		Dictionary<FastVec2i, ColorAndZoom> changes = [];
		Dictionary<RegionPosition, MapRegion> storedRegions = this.serverStorage![player.PlayerUID].Regions;

		foreach(FastVec2i pos in Iterators.Circle(chunkPosition, radius)) {
			RegionPosition position = RegionPosition.FromChunkPosition(pos);
			MapRegion region = storedRegions.GetOrCreate(position);
			byte? newZoomLevel = region.SetColorAndZoomLevels(pos, colorLevel, zoomLevel, forceOverdraw);
			if(newZoomLevel == null)
				continue;

			changes[pos] = new ColorAndZoom(colorLevel, newZoomLevel.Value);
			durability -= MapChunk.Area >> (newZoomLevel.Value * 2);
			if(durability <= 0)
				break;
		}

		if(changes.Count > 0)
			this.mapSink.SendMapDataToClient(this, player, SerializerUtil.Serialize(new ServerToClientPacket{Changes = changes}));
		return durability;
	}

	public override void OnViewChangedServer(IServerPlayer player, int x1, int z1, int x2, int z2) {
		if(this.joiningPlayers!.Count == 0)
			return;

		string uid = player.PlayerUID;
		if(this.joiningPlayers.Remove(uid))
			this.serverStorage!.GetOrCreate(uid);
	}

	public override void OnDataFromServer(byte[] data) {
		ServerToClientPacket packet = SerializerUtil.Deserialize<ServerToClientPacket>(data);
		lock(this.chunksToRedrawLock!)
			this.UpdateChunks(packet.Changes);
	}

	private void UpdateChunks(Dictionary<FastVec2i, ColorAndZoom> changes) {
		ConcurrentQueue<ReadyMapPiece> readyMapPieces = MapperChunkMapLayer.readyMapPieces.GetValue(this);
		foreach(KeyValuePair<FastVec2i, ColorAndZoom> item in changes) {
			if(!this.clientStorage!.Chunks.ContainsKey(item.Key) || item.Value.Color == 0) {
				this.clientStorage.Chunks[item.Key] = new MapChunk();
				readyMapPieces.Enqueue(new ReadyMapPiece{Cord = item.Key, Pixels = MapChunk.UnexploredPixels});
			}
			if(item.Value.Color > 0)
				this.clientStorage.ChunksToRedraw.Enqueue(item);
		}
	}

	public override void OnOffThreadTick(float dt) {
		if(this.api.Side == EnumAppSide.Server)
			return;

		this.lastThreadUpdateTime += dt;
		if(this.lastThreadUpdateTime < 0.1)
			return;
		this.lastThreadUpdateTime = 0;

		this.CheckChunksToRedraw();
		this.ProcessMappedChunks();
	}

	private void CheckChunksToRedraw() {
		ConcurrentQueue<ReadyMapPiece> readyMapPieces = MapperChunkMapLayer.readyMapPieces.GetValue(this);
		IBlockAccessor blockAccessor = this.api.World.BlockAccessor;
		for(int count = this.clientStorage!.ChunksToRedraw.Count; count > 0 && !this.mapSink.IsShuttingDown; --count) {
			KeyValuePair<FastVec2i, ColorAndZoom> redrawRequest;
			lock(this.chunksToRedrawLock!) {
				if(this.clientStorage.ChunksToRedraw.Count == 0)
					break;
				redrawRequest = this.clientStorage.ChunksToRedraw.Dequeue();
			}

			IMapChunk? chunk = blockAccessor.GetMapChunk(redrawRequest.Key.X, redrawRequest.Key.Y);
			int[]? pixels = chunk != null ? this.GenerateChunkImage(redrawRequest.Key, chunk, redrawRequest.Value.Color == 3) : null;
			if(pixels == null) {
				lock(this.chunksToRedrawLock)
					this.clientStorage.ChunksToRedraw.Enqueue(redrawRequest);
				continue;
			}
			if(redrawRequest.Value.Color == 1)
				MapperChunkMapLayer.ConvertToGrayscale(pixels, (uint)this.colorsByCode.Get("ocean", 0) | 0xFF000000);
			if(redrawRequest.Value.ZoomLevel > 0)
				MapperChunkMapLayer.ApplyBoxFilter(pixels, redrawRequest.Value.ZoomLevel);

			this.clientStorage.Chunks[redrawRequest.Key] = new MapChunk(pixels);
			readyMapPieces.Enqueue(new ReadyMapPiece{Cord = redrawRequest.Key, Pixels = pixels});
		}
	}

	private void ProcessMappedChunks() {
		UniqueQueue<FastVec2i> chunksToGen = MapperChunkMapLayer.chunksToGen.GetValue(this);
		object chunksToGenLock = MapperChunkMapLayer.chunksToGenLock.GetValue(this);
		ConcurrentQueue<ReadyMapPiece> readyMapPieces = MapperChunkMapLayer.readyMapPieces.GetValue(this);

		for(int count = chunksToGen.Count; count > 0 && !this.mapSink.IsShuttingDown; --count) {
			FastVec2i chunkPosition;
			lock(chunksToGenLock) {
				if(chunksToGen.Count == 0)
					break;
				chunkPosition = chunksToGen.Dequeue();
			}

			if(this.clientStorage!.Chunks.TryGetValue(chunkPosition, out MapChunk mapChunk))
				readyMapPieces.Enqueue(new ReadyMapPiece{Cord = chunkPosition, Pixels = mapChunk.Pixels});
		}
	}

	public static MapperChunkMapLayer GetInstance(ICoreAPI api) {
		return api.ModLoader.GetModSystem<MapperModSystem>().mapLayer!;
	}

	private static void ConvertToGrayscale(int[] pixels, uint oceanColor) {
		const uint paperColor = 0xFF98CCDC;
		for(int i = 0; i < pixels.Length; ++i) {
			uint color = (uint)pixels[i];
			float alpha = color == oceanColor ? 1 : ((color & 0xFF) * 0.29891f + ((color >> 8) & 0xFF) * 0.58661f + ((color >> 16) & 0xFF) * 0.11448f) / 255f;
			uint r = (byte)(alpha * (paperColor & 0xFF));
			uint g = (byte)(alpha * ((paperColor >> 8) & 0xFF));
			uint b = (byte)(alpha * ((paperColor >> 16) & 0xFF));
			pixels[i] = (int)(r | (g << 8) | (b << 16) | 0xFF000000);
		}
	}

	private static void ApplyBoxFilter(int[] pixels, byte zoomLevel) {
		uint resolution = 1u << zoomLevel;
		uint resolutionSquared = resolution * resolution;
		for(uint y = 0; y < MapChunk.Size; y += resolution)
			for(uint x = 0; x < MapChunk.Size; x += resolution) {
				uint sumR = 0, sumG = 0, sumB = 0;
				for(uint innerY = 0; innerY < resolution; ++innerY) {
					uint rowOffset = (y + innerY) * MapChunk.Size + x;
					for(uint innerX = 0; innerX < resolution; ++innerX) {
						uint color = (uint)pixels[rowOffset + innerX];
						sumR += color & 0xFF;
						sumG += (color >> 8) & 0xFF;
						sumB += (color >> 16) & 0xFF;
					}
				}

				uint r = sumR / resolutionSquared;
				uint g = sumG / resolutionSquared;
				uint b = sumB / resolutionSquared;
				for(uint innerY = 0; innerY < resolution; ++innerY) {
					uint rowOffset = (y + innerY) * MapChunk.Size + x;
					for(uint innerX = 0; innerX < resolution; ++innerX)
						pixels[rowOffset + innerX] = (int)(0xFF000000 | (b << 16) | (g << 8) | r);
				}
			}
	}
}
