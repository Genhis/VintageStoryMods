namespace Mapper.WorldMap;

using Mapper.Behaviors;
using Mapper.Util;
using Mapper.Util.Reflection;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.API.Util;
using Vintagestory.GameContent;

public class MapperChunkMapLayer : ChunkMapLayer {
	private const int ClientAutosaveTime = 60 * 5;
	private static readonly FieldAccessor<ChunkMapLayer, UniqueQueue<FastVec2i>> chunksToGen = new("chunksToGen");
	private static readonly FieldAccessor<ChunkMapLayer, object> chunksToGenLock = new("chunksToGenLock");
	private static readonly FieldAccessor<ChunkMapLayer, ConcurrentQueue<ReadyMapPiece>> readyMapPieces = new("readyMapPieces");

	// Server variables
	private readonly ServerMapStorage? serverStorage;
	private readonly HashSet<string>? joiningPlayers;

	// Client variables
	private readonly ClientMapStorage? clientStorage;
	private readonly string? clientStorageFilename;
	private readonly object? chunksToRedrawLock;
	private Vec3d? lastKnownPosition;
	private float lastThreadUpdateTime;
	private float clientAutosaveTimer;

	private bool dirty;
	private bool enabled = true;

	public override EnumMapAppSide DataSide => EnumMapAppSide.Server;

	public MapperChunkMapLayer(ICoreAPI api, IWorldMapManager mapSink) : base(api, mapSink) {
		if(api is ICoreServerAPI sapi) {
			this.serverStorage = [];
			this.joiningPlayers = [];

			sapi.Event.GameWorldSave += () => {
				if(this.dirty)
					this.dirty = this.serverStorage.Save((ICoreServerAPI)this.api);
			};
			sapi.Event.PlayerJoin += player => this.joiningPlayers.Add(player.PlayerUID);
		}
		else {
			this.clientStorage = new ClientMapStorage();
			this.clientStorageFilename = MapperChunkMapLayer.GetClientStorageFilename(api);
			this.chunksToRedrawLock = new();

			this.enabled = this.clientStorage.Load(this.clientStorageFilename, api.Logger);
		}

		this.api.ChatCommands.GetOrCreate("mapper").RequiresPrivilege(Privilege.root).BeginSubCommand("enable").WithDescription(Lang.Get("mapper:commanddesc-mapper-enable")).HandleWith(this.HandleEnableCommand);
	}

	public override void OnLoaded() {
		base.OnLoaded();

		MapperModSystem modSystem = this.api.ModLoader.GetModSystem<MapperModSystem>();
		if(modSystem.mapLayer != null)
			throw new InvalidOperationException("Another MapperChunkMapLayer instance is already loaded");
		modSystem.mapLayer = this;

		if(this.serverStorage != null)
			this.enabled = this.serverStorage.Load((ICoreServerAPI)this.api);
	}

	public override void OnShutDown() {
		if(this.clientStorage != null) {
			if(this.dirty)
				this.clientStorage.Save(this.clientStorageFilename!, this.api.Logger, ref this.dirty);
			this.clientStorage.Dispose();
		}
		this.api.ModLoader.GetModSystem<MapperModSystem>().mapLayer = null;
		base.OnShutDown();
	}

	private TextCommandResult HandleEnableCommand(TextCommandCallingArgs args) {
		string side = this.api.Side == EnumAppSide.Client ? "client" : "server";
		if(this.enabled)
			return TextCommandResult.Error(Lang.Get($"mapper:commandresult-mapper-enable-{side}-error"));

		if(this.api is ICoreClientAPI capi)
			this.mapSink.SendMapDataToServer(this, SerializerUtil.Serialize(new ClientToServerPacket{PlayerUID = capi.World.Player.PlayerUID, RecoverMap = true}));
		else
			this.enabled = true;
		return TextCommandResult.Success(Lang.Get($"mapper:commandresult-mapper-enable-{side}-success"));
	}

	public int MarkChunksForRedraw(IServerPlayer player, FastVec2i chunkPosition, int radius, int durability, byte colorLevel, byte zoomLevel, bool forceOverdraw = false) {
		if(!this.CheckEnabledServer(player))
			return durability;

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

		if(changes.Count > 0) {
			this.dirty = true;
			this.mapSink.SendMapDataToClient(this, player, SerializerUtil.Serialize(new ServerToClientPacket{Changes = changes}));
		}
		return durability;
	}

	public bool UpdateLastKnownPosition(Vec3d? position) {
		if((position == null) == (this.lastKnownPosition == null))
			return false;

		this.lastKnownPosition = position?.Clone();
		this.mapSink.SendMapDataToServer(this, SerializerUtil.Serialize(new ClientToServerPacket{PlayerUID = ((ICoreClientAPI)this.api).World.Player.PlayerUID, LastKnownPosition = this.lastKnownPosition}));
		return true;
	}

	public override void OnViewChangedServer(IServerPlayer player, int x1, int z1, int x2, int z2) {
		if(this.joiningPlayers!.Count == 0)
			return;

		string uid = player.PlayerUID;
		if(this.joiningPlayers.Remove(uid))
			this.mapSink.SendMapDataToClient(this, player, SerializerUtil.Serialize(new ServerToClientPacket{LastKnownPosition = this.serverStorage!.GetOrCreate(uid).LastKnownPosition}));
	}

	public override void OnDataFromClient(byte[] data) {
		if(!this.enabled)
			return;

		ClientToServerPacket packet = SerializerUtil.Deserialize<ClientToServerPacket>(data);
		if(packet.RecoverMap)
			this.mapSink.SendMapDataToClient(this, (IServerPlayer)this.api.World.PlayerByUid(packet.PlayerUID), SerializerUtil.Serialize(new ServerToClientPacket{Changes = this.serverStorage![packet.PlayerUID].PrepareClientRecovery(), RecoverMap = true}));
		else {
			this.dirty = true;
			this.serverStorage![packet.PlayerUID].LastKnownPosition = packet.LastKnownPosition;
		}
	}

	public override void OnDataFromServer(byte[] data) {
		ServerToClientPacket packet = SerializerUtil.Deserialize<ServerToClientPacket>(data);
		if(packet.RecoverMap) {
			this.enabled = true;
			if(packet.Changes == null)
				return;
		}
		if(!this.enabled)
			return;

		if(packet.Changes == null) {
			this.lastKnownPosition = packet.LastKnownPosition;
			if(this.mapSink is WorldMapManager manager)
				if(this.lastKnownPosition != null && manager.worldMapDlg?.DialogType == EnumDialogType.HUD)
					manager.worldMapDlg.TryClose();
			return;
		}

		lock(this.chunksToRedrawLock!)
			this.UpdateChunks(packet.Changes);
	}

	private void UpdateChunks(Dictionary<FastVec2i, ColorAndZoom> changes) {
		ConcurrentQueue<ReadyMapPiece> readyMapPieces = MapperChunkMapLayer.readyMapPieces.GetValue(this);
		using IDisposable guard = this.clientStorage!.SaveLock.SharedLock();
		this.dirty = true;
		foreach(KeyValuePair<FastVec2i, ColorAndZoom> item in changes) {
			if(!this.clientStorage!.Chunks.ContainsKey(item.Key) || item.Value.Color == 0) {
				this.clientStorage.Chunks[item.Key] = new MapChunk();
				readyMapPieces.Enqueue(new ReadyMapPiece{Cord = item.Key, Pixels = MapChunk.UnexploredPixels});
			}
			if(item.Value.Color > 0)
				this.clientStorage.ChunksToRedraw.Enqueue(item);
		}
	}

	public override void OnTick(float dt) {
		base.OnTick(dt);
		if(this.lastKnownPosition != null)
			this.CheckLastKnownPosition();
	}

	public override void OnOffThreadTick(float dt) {
		if(this.api.Side == EnumAppSide.Server)
			return;

		if(this.dirty) {
			this.clientAutosaveTimer += dt;
			if(this.clientAutosaveTimer > MapperChunkMapLayer.ClientAutosaveTime) {
				this.clientAutosaveTimer = 0;
				this.clientStorage!.Save(this.clientStorageFilename!, this.api.Logger, ref this.dirty);
			}
		}

		this.lastThreadUpdateTime += dt;
		if(this.lastThreadUpdateTime < 0.1)
			return;
		this.lastThreadUpdateTime = 0;

		this.CheckChunksToRedraw();
		this.ProcessMappedChunks();
	}

	private void CheckLastKnownPosition() {
		ICoreClientAPI capi = (ICoreClientAPI)this.api;
		IClientPlayer player = capi.World.Player;
		if(this.GetScaleFactor(player, player.Entity.Pos.ToChunkPosition()) == null)
			return;

		this.UpdateLastKnownPosition(null);
		if(this.mapSink is WorldMapManager manager)
			if(!manager.worldMapDlg.IsOpened() && capi.Settings.Bool["showMinimapHud"])
				manager.ToggleMap(EnumDialogType.HUD);
	}

	private void CheckChunksToRedraw() {
		int count = this.clientStorage!.ChunksToRedraw.Count;
		if(count == 0)
			return;

		ConcurrentQueue<ReadyMapPiece> readyMapPieces = MapperChunkMapLayer.readyMapPieces.GetValue(this);
		IBlockAccessor blockAccessor = this.api.World.BlockAccessor;
		using IDisposable guard = this.clientStorage.SaveLock.SharedLock();
		for(; count > 0 && !this.mapSink.IsShuttingDown; --count) {
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

			this.dirty = true;
			this.clientStorage.Chunks[redrawRequest.Key] = new MapChunk(pixels, redrawRequest.Value.ZoomLevel);
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

	public bool CheckEnabledClient() {
		if(this.enabled)
			return true;

		((ICoreClientAPI)this.api).TriggerIngameError(this, "mapper-mod-disabled", Lang.Get("mapper:error-mod-disabled-client"));
		return false;
	}

	public bool CheckEnabledServer(IServerPlayer player) {
		if(this.enabled)
			return true;

		player.SendMessage(GlobalConstants.InfoLogChatGroup, Lang.GetL(player.LanguageCode, "mapper:error-mod-disabled-server"), EnumChatType.Notification);
		return false;
	}

	public int? GetScaleFactor(IServerPlayer player, FastVec2i chunkPosition) {
		if(this.serverStorage![player.PlayerUID].Regions.TryGetValue(RegionPosition.FromChunkPosition(chunkPosition), out MapRegion mapRegion)) {
			byte zoomLevel = mapRegion.GetZoomLevel(chunkPosition);
			return zoomLevel == ColorAndZoom.EmptyZoomLevel ? null : 1 << zoomLevel;
		}
		return null;
	}

	public int? GetScaleFactor(IClientPlayer? player, FastVec2i chunkPosition) {
		static int? GetCompassScaleFactor(ItemSlot slot) => BehaviorCompassNeedle.GetScaleFactor(slot.Itemstack?.ItemAttributes);

		int? scaleFactor = this.clientStorage!.Chunks.TryGetValue(chunkPosition, out MapChunk mapChunk) ? 1 << mapChunk.ZoomLevel : null;
		if(player != null)
			scaleFactor = MathUtil.Min(scaleFactor, MathUtil.Min(GetCompassScaleFactor(player.Entity.LeftHandItemSlot), GetCompassScaleFactor(player.Entity.RightHandItemSlot)));
		return scaleFactor;
	}

	public Vec3d GetPlayerOrLastKnownPosition() {
		if(this.lastKnownPosition != null)
			return this.lastKnownPosition;

		IClientPlayer player = ((ICoreClientAPI)this.api).World.Player;
		EntityPos entityPos = player.Entity.Pos;
		return MapperChunkMapLayer.ClampPosition(entityPos.XYZ, this.GetScaleFactor(player, entityPos.ToChunkPosition()) ?? 1);
	}

	public static MapperChunkMapLayer GetInstance(ICoreAPI api) {
		return api.ModLoader.GetModSystem<MapperModSystem>().mapLayer!;
	}

	public static Vec3d ClampPosition(Vec3d position, int scaleFactor) {
		if(scaleFactor == 1)
			return position;

		position.X = (int)position.X / scaleFactor * scaleFactor + scaleFactor / 2;
		position.Y = (int)position.Y / scaleFactor * scaleFactor + scaleFactor / 2;
		position.Z = (int)position.Z / scaleFactor * scaleFactor + scaleFactor / 2;
		return position;
	}

	public static bool HasLastKnownPosition(ICoreAPI api) {
		return MapperChunkMapLayer.GetInstance(api).lastKnownPosition != null;
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

	private static string GetClientStorageFilename(ICoreAPI api) {
		string directory = Path.Combine(GamePaths.DataPath, "Maps", "MapperMod");
		GamePaths.EnsurePathExists(directory);
		return Path.Combine(directory, api.World.SavegameIdentifier + ".dat");
	}
}
