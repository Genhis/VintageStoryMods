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

		ConcurrentQueue<ReadyMapPiece> readyMapPieces = MapperChunkMapLayer.readyMapPieces.GetValue(this);
		foreach(KeyValuePair<FastVec2i, ColorAndZoom> item in packet.Changes) {
			if(!this.clientStorage!.Chunks.ContainsKey(item.Key)) {
				this.clientStorage.Chunks[item.Key] = new MapChunk();
				readyMapPieces.Enqueue(new ReadyMapPiece{Cord = item.Key, Pixels = MapChunk.UnexploredPixels});
			}
		}
	}

	public override void OnOffThreadTick(float dt) {
		if(this.api.Side == EnumAppSide.Server)
			return;

		this.lastThreadUpdateTime += dt;
		if(this.lastThreadUpdateTime < 0.1)
			return;
		this.lastThreadUpdateTime = 0;

		this.ProcessMappedChunks();
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
}
