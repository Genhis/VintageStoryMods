namespace Mapper.WorldMap;

using Mapper.Behaviors;
using Mapper.Blocks;
using Mapper.Util;
using Mapper.Util.Reflection;
using Mapper.Util.IO;
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
	private MapBackground? background;
	private Vec3d? lastKnownPosition;
	private float lastThreadUpdateTime;
	private float clientAutosaveTimer;

	private ILogger logger;
	private bool dirty;
	private enum Status { Enabled, DisabledMap, CorruptedData }
	private Status status = Status.Enabled;

	public override EnumMapAppSide DataSide => EnumMapAppSide.Server;
	public bool Enabled => this.status == Status.Enabled;
	public readonly Dictionary<object, Action<FastVec2i>> OnChunkChanged = [];

	public MapperChunkMapLayer(ICoreAPI api, IWorldMapManager mapSink) : base(api, mapSink) {
		this.logger = api.Logger;
		if(api is ICoreServerAPI sapi) {
			this.serverStorage = [];
			this.joiningPlayers = [];

			sapi.Event.GameWorldSave += () => {
				if(this.dirty && this.serverStorage.Save((ICoreServerAPI)this.api, this.logger))
					this.dirty = false;
			};
			sapi.Event.PlayerJoin += player => this.joiningPlayers.Add(player.PlayerUID);
		}
		else {
			this.clientStorage = new ClientMapStorage();
			this.clientStorageFilename = MapperChunkMapLayer.GetClientStorageFilename(api);
			this.chunksToRedrawLock = new();
		}

		this.api.ChatCommands.GetOrCreate("mapper").RequiresPrivilege(Privilege.root).BeginSubCommand("restore").WithDescription(Lang.Get("mapper:commanddesc-mapper-restore")).HandleWith(this.HandleRestoreCommand);
	}

	public override void OnLoaded() {
		base.OnLoaded();

		MapperModSystem modSystem = this.api.ModLoader.GetModSystem<MapperModSystem>();
		if(modSystem.mapLayer != null)
			throw new InvalidOperationException("Another MapperChunkMapLayer instance is already loaded");
		modSystem.mapLayer = this;
		this.logger = modSystem.Mod.Logger;

		if(!this.api.World.Config.GetBool("allowMap", true)) {
			this.status = Status.DisabledMap;
			this.logger.Warning("World map is disabled, run `/worldconfig allowMap true` to enable it and restart the server");
			return;
		}

		if(this.serverStorage != null)
			this.status = this.serverStorage.Load((ICoreServerAPI)this.api, this.logger) ? Status.Enabled : Status.CorruptedData;
		else {
			this.background = new MapBackground((ICoreClientAPI)this.api, this.logger, "mapper:textures/map.png");
			this.status = this.clientStorage!.Load(this.clientStorageFilename!, this.logger, this.background) ? Status.Enabled : Status.CorruptedData;
		}
	}

	public override void OnShutDown() {
		if(this.clientStorage != null) {
			if(this.dirty)
				this.clientStorage.Save(this.clientStorageFilename!, this.logger, ref this.dirty);
			this.clientStorage.Dispose();
		}
		this.api.ModLoader.GetModSystem<MapperModSystem>().mapLayer = null;
		base.OnShutDown();
	}

	private TextCommandResult HandleRestoreCommand(TextCommandCallingArgs args) {
		string side = this.api.Side == EnumAppSide.Client ? "client" : "server";
		if(this.status != Status.CorruptedData)
			return TextCommandResult.Error(Lang.Get($"mapper:commandresult-mapper-restore-{side}-error"));

		if(this.api is ICoreClientAPI capi)
			this.mapSink.SendMapDataToServer(this, SerializerUtil.Serialize(new ClientToServerPacket { PlayerUID = capi.World.Player.PlayerUID, RecoverMap = true }));
		else
			this.status = Status.Enabled;
		return TextCommandResult.Success(Lang.Get($"mapper:commandresult-mapper-restore-{side}-success"));
	}

	public void SendSyncWithTableRequest(BlockPos tablePos) {
		if(this.api is not ICoreClientAPI capi)
			return;

		byte[]? sharedMapData = null;
		if(this.clientStorage != null) {
			using MemoryStream stream = new();
			using(VersionedWriter output = VersionedWriter.Create(stream, leaveOpen: true, compressed: true)) {
				output.Write(this.clientStorage.Chunks);
			}
			sharedMapData = stream.ToArray();
		}
		this.mapSink.SendMapDataToServer(this, SerializerUtil.Serialize(new ClientToServerPacket {
			PlayerUID = capi.World.Player.PlayerUID,
			SyncWithTablePos = tablePos,
			ShareMapData = sharedMapData
		}));
	}

	private void ExecuteSyncWithTable(IServerPlayer player, BlockPos tablePos, byte[] playerMapData) {
		ICoreServerAPI sapi = (ICoreServerAPI)this.api;

		BlockEntity? be = sapi.World.BlockAccessor.GetBlockEntity(tablePos);

		if(be is not BlockEntityCartographersTable table) {
			player.SendMessage(GlobalConstants.InfoLogChatGroup, Lang.Get("mapper:error-cartographers-table-not-found"), EnumChatType.Notification);
			return;
		}
		ServerPlayerMap serverPlayerMap = this.serverStorage!.GetOrCreate(player.PlayerUID);

		// Get player's current waypoints from the game
		List<Waypoint> playerWaypoints = WaypointHelper.GetPlayerWaypoints(sapi, player.PlayerUID);

		(byte[]? mapData, int uploadedWaypoints) = table.SynchronizeMap(playerMapData, serverPlayerMap, playerWaypoints, this.background!, ref this.dirty);
		this.dirty = true;

		// Replace player's waypoints with all waypoints from the table
		int downloadedWaypoints = WaypointHelper.ReplacePlayerWaypoints(sapi, player, table.Waypoints);

		// Build upload message
		if(mapData != null || uploadedWaypoints > 0) {
			if(mapData != null && uploadedWaypoints > 0)
				player.SendLocalisedMessage(0, Lang.Get("mapper:commandresult-cartographers-table-uploaded-both", uploadedWaypoints));
			else if(mapData != null)
				player.SendLocalisedMessage(0, Lang.Get("mapper:commandresult-cartographers-table-uploaded-map"));
			else
				player.SendLocalisedMessage(0, Lang.Get("mapper:commandresult-cartographers-table-uploaded-waypoints", uploadedWaypoints));
		}
		else {
			player.SendLocalisedMessage(0, Lang.Get("mapper:commandresult-cartographers-table-uploaded-nothing"));
		}

		this.mapSink.SendMapDataToClient(this, player, SerializerUtil.Serialize(new ServerToClientPacket {
			SharedMapData = mapData,
			DownloadedWaypoints = downloadedWaypoints
		}));
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
			this.mapSink.SendMapDataToClient(this, player, SerializerUtil.Serialize(new ServerToClientPacket { Changes = changes }));
		}
		return durability;
	}

	public bool UpdateLastKnownPosition(Vec3d? position) {
		if((position == null) == (this.lastKnownPosition == null))
			return false;

		this.lastKnownPosition = position?.Clone();
		this.mapSink.SendMapDataToServer(this, SerializerUtil.Serialize(new ClientToServerPacket { PlayerUID = ((ICoreClientAPI)this.api).World.Player.PlayerUID, LastKnownPosition = this.lastKnownPosition }));
		return true;
	}

	public override void OnViewChangedServer(IServerPlayer player, int x1, int z1, int x2, int z2) {
		if(this.joiningPlayers!.Count == 0)
			return;

		string uid = player.PlayerUID;
		if(this.joiningPlayers.Remove(uid)) {
			this.logger.Notification($"Sending last known position to player {uid}");
			this.mapSink.SendMapDataToClient(this, player, SerializerUtil.Serialize(new ServerToClientPacket{LastKnownPosition = this.serverStorage!.GetOrCreate(uid).LastKnownPosition}));
		}
	}

	public override void OnDataFromClient(byte[] data) {
		ClientToServerPacket packet = SerializerUtil.Deserialize<ClientToServerPacket>(data);
		if(!this.Enabled) {
			this.CheckEnabledServer((IServerPlayer)this.api.World.PlayerByUid(packet.PlayerUID));
			return;
		}

		if(packet.RecoverMap)
			this.mapSink.SendMapDataToClient(this, (IServerPlayer)this.api.World.PlayerByUid(packet.PlayerUID), SerializerUtil.Serialize(new ServerToClientPacket { Changes = this.serverStorage![packet.PlayerUID].PrepareClientRecovery(), RecoverMap = true }));
		else if(packet.SyncWithTablePos != null && packet.ShareMapData != null) {
			IServerPlayer player = (IServerPlayer)this.api.World.PlayerByUid(packet.PlayerUID);
			this.ExecuteSyncWithTable(player, packet.SyncWithTablePos, packet.ShareMapData);
		}
		else {
			this.dirty = true;
			this.serverStorage![packet.PlayerUID].LastKnownPosition = packet.LastKnownPosition;
		}
	}

	public override void OnDataFromServer(byte[] data) {
		ServerToClientPacket packet = SerializerUtil.Deserialize<ServerToClientPacket>(data);
		if(packet.RecoverMap && this.status == Status.CorruptedData) {
			this.status = Status.Enabled;
			((ICoreClientAPI)this.api).World.Player.ShowChatNotification(Lang.Get("mapper:commandresult-mapper-restore-client-request-response"));
			if(packet.Changes == null)
				return;
		}
		if(!this.Enabled)
			return;

		if(packet.LastKnownPosition != null) {
			this.lastKnownPosition = packet.LastKnownPosition;
			if(this.mapSink is WorldMapManager manager)
				if(this.lastKnownPosition != null && manager.worldMapDlg?.DialogType == EnumDialogType.HUD)
					manager.worldMapDlg.TryClose();
		}

		if(packet.Changes != null)
			lock(this.chunksToRedrawLock!)
				this.UpdateChunks(packet.Changes);


		if(packet.SharedMapData != null) {
			int mergedCount = 0;
			using VersionedReader input = VersionedReader.Create(new MemoryStream(packet.SharedMapData, false), compressed: true);
			Dictionary<FastVec2i, MapChunk> chunks = [];
			input.ReadChunks(chunks, this.background!);
			mergedCount = BlockEntityCartographersTable.MergeChunks(this.clientStorage!.Chunks, chunks, this.clientStorage!.Chunks);
			if(mergedCount > 0) {
				this.dirty = true;
			}

			// Build upload message
			if(mergedCount > 0 || packet.DownloadedWaypoints > 0) {
				if(mergedCount > 0 && packet.DownloadedWaypoints > 0)
					((ICoreClientAPI)this.api).World.Player.ShowChatNotification(Lang.Get("mapper:commandresult-cartographers-table-downloaded-both", packet.DownloadedWaypoints));
				else if(mergedCount > 0)
					((ICoreClientAPI)this.api).World.Player.ShowChatNotification(Lang.Get("mapper:commandresult-cartographers-table-downloaded-map"));
				else
					((ICoreClientAPI)this.api).World.Player.ShowChatNotification(Lang.Get("mapper:commandresult-cartographers-table-downloaded-waypoints", packet.DownloadedWaypoints));
			}
			else {
				((ICoreClientAPI)this.api).World.Player.ShowChatNotification(Lang.Get("mapper:commandresult-cartographers-table-downloaded-nothing"));
			}
		}
	}

	private void UpdateChunks(Dictionary<FastVec2i, ColorAndZoom> changes) {
		ConcurrentQueue<ReadyMapPiece> readyMapPieces = MapperChunkMapLayer.readyMapPieces.GetValue(this);
		using IDisposable guard = this.clientStorage!.SaveLock.SharedLock();
		this.dirty = true;
		foreach(KeyValuePair<FastVec2i, ColorAndZoom> item in changes) {
			if(!this.clientStorage!.Chunks.ContainsKey(item.Key) || item.Value.Color == 0) {
				int[] pixels = this.background!.GetPixels(item.Key, item.Value.ZoomLevel);
				this.clientStorage.Chunks[item.Key] = new MapChunk(pixels, item.Value.ZoomLevel, 0); // 0 = unexplored/background
				readyMapPieces.Enqueue(new ReadyMapPiece { Cord = item.Key, Pixels = pixels });
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
				this.clientStorage!.Save(this.clientStorageFilename!, this.logger, ref this.dirty);
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
				MapperChunkMapLayer.ConvertToGrayscale(pixels, this.background!.GetPixels(redrawRequest.Key, redrawRequest.Value.ZoomLevel), (uint)this.colorsByCode.Get("ocean", 0) | 0xFF000000);
			if(redrawRequest.Value.ZoomLevel > 0)
				MapperChunkMapLayer.ApplyBoxFilter(pixels, 1u << redrawRequest.Value.ZoomLevel);

			this.dirty = true;
			this.clientStorage.Chunks[redrawRequest.Key] = new MapChunk(pixels, redrawRequest.Value.ZoomLevel, redrawRequest.Value.Color);
			readyMapPieces.Enqueue(new ReadyMapPiece { Cord = redrawRequest.Key, Pixels = pixels });

			if(this.OnChunkChanged.Count != 0)
				lock(this.OnChunkChanged)
					foreach(KeyValuePair<object, Action<FastVec2i>> item in this.OnChunkChanged)
						item.Value.Invoke(redrawRequest.Key);
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
				readyMapPieces.Enqueue(new ReadyMapPiece { Cord = chunkPosition, Pixels = mapChunk.Pixels });
		}
	}

	public bool CheckEnabledClient() {
		if(this.Enabled)
			return true;

		((ICoreClientAPI)this.api).TriggerIngameError(this, "mapper-mod-disabled", Lang.Get("mapper:error-" + MapperChunkMapLayer.GetStatusCode(this.status, true)));
		return false;
	}

	public bool CheckEnabledServer(IServerPlayer player) {
		if(this.Enabled)
			return true;

		player.SendMessage(GlobalConstants.InfoLogChatGroup, Lang.GetL(player.LanguageCode, "mapper:error-" + MapperChunkMapLayer.GetStatusCode(this.status, false)), EnumChatType.Notification);
		return false;
	}

	public int? GetScaleFactor(IServerPlayer player, FastVec2i chunkPosition) {
		if(this.Enabled && this.serverStorage![player.PlayerUID].Regions.TryGetValue(RegionPosition.FromChunkPosition(chunkPosition), out MapRegion mapRegion)) {
			byte zoomLevel = mapRegion.GetZoomLevel(chunkPosition);
			return zoomLevel == ColorAndZoom.EmptyZoomLevel ? null : 1 << zoomLevel;
		}
		return null;
	}

	public int? GetScaleFactor(IClientPlayer? player, FastVec2i chunkPosition) {
		static int? GetCompassScaleFactor(ItemSlot slot) => BehaviorCompassNeedle.GetScaleFactor(slot.Itemstack?.ItemAttributes);

		int? scaleFactor = this.Enabled && this.clientStorage!.Chunks.TryGetValue(chunkPosition, out MapChunk mapChunk) ? 1 << mapChunk.ZoomLevel : null;
		if(player != null)
			scaleFactor = MathUtil.Min(scaleFactor, MathUtil.Min(GetCompassScaleFactor(player.Entity.LeftHandItemSlot), GetCompassScaleFactor(player.Entity.RightHandItemSlot)));
		return scaleFactor;
	}

	public bool HasLastKnownPosition() {
		return this.lastKnownPosition != null;
	}

	public Vec3d GetPlayerOrLastKnownPosition() {
		if(this.lastKnownPosition != null)
			return this.lastKnownPosition;

		IClientPlayer player = ((ICoreClientAPI)this.api).World.Player;
		EntityPos entityPos = player.Entity.Pos;
		return MapperChunkMapLayer.ClampPosition(entityPos.XYZ, this.GetScaleFactor(player, entityPos.ToChunkPosition()) ?? 1);
	}

	public void TrySendUnrevealedMapMessage(IServerPlayer player) {
		ServerPlayerMap playerData = this.serverStorage![player.PlayerUID];
		long elapsedTime = player.Entity.World.ElapsedMilliseconds;
		if(playerData.NextUnrevealedMapMessage > elapsedTime)
			return;

		playerData.NextUnrevealedMapMessage = elapsedTime + 10000;
		player.SendMessage(GlobalConstants.InfoLogChatGroup, Lang.GetL(player.LanguageCode, "mapper:error-unexplored-map"), EnumChatType.Notification);
	}

	internal static MapperChunkMapLayer GetInstance(ICoreAPI api) {
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

	private static void ConvertToGrayscale(int[] pixels, int[] paperPixels, uint oceanColor) {
		for(int i = 0; i < pixels.Length; ++i) {
			uint color = (uint)pixels[i];
			uint paperColor = (uint)paperPixels[i];
			float alpha = color == oceanColor ? 1 : ((color & 0xFF) * 0.29891f + ((color >> 8) & 0xFF) * 0.58661f + ((color >> 16) & 0xFF) * 0.11448f) / 255f;
			uint r = (byte)(alpha * (paperColor & 0xFF));
			uint g = (byte)(alpha * ((paperColor >> 8) & 0xFF));
			uint b = (byte)(alpha * ((paperColor >> 16) & 0xFF));
			pixels[i] = (int)(r | (g << 8) | (b << 16) | 0xFF000000);
		}
	}

	internal static int[] ApplyBoxFilter(int[] pixels, uint resolution) {
		uint resolutionSquared = resolution * resolution;
		for(uint y = 0; y < MapChunk.Size; y += resolution)
			for(uint x = 0; x < MapChunk.Size; x += resolution) {
				uint sumR = 0, sumG = 0, sumB = 0, sumA = 0;
				for(uint innerY = 0; innerY < resolution; ++innerY) {
					uint rowOffset = (y + innerY) * MapChunk.Size + x;
					for(uint innerX = 0; innerX < resolution; ++innerX) {
						uint color = (uint)pixels[rowOffset + innerX];
						sumR += color & 0xFF;
						sumG += (color >> 8) & 0xFF;
						sumB += (color >> 16) & 0xFF;
						sumA += color >> 24;
					}
				}

				int pixel = (int)(sumA / resolutionSquared << 24 | sumB / resolutionSquared << 16 | sumG / resolutionSquared << 8 | sumR / resolutionSquared);
				for(uint innerY = 0; innerY < resolution; ++innerY) {
					uint rowOffset = (y + innerY) * MapChunk.Size + x;
					for(uint innerX = 0; innerX < resolution; ++innerX)
						pixels[rowOffset + innerX] = pixel;
				}
			}
		return pixels;
	}

	private static string GetClientStorageFilename(ICoreAPI api) {
		string directory = Path.Combine(GamePaths.DataPath, "Maps", "MapperMod");
		GamePaths.EnsurePathExists(directory);
		return Path.Combine(directory, api.World.SavegameIdentifier + ".dat");
	}

	private static string GetStatusCode(Status status, bool client) {
		return status switch {
			Status.DisabledMap => "mod-disabled",
			Status.CorruptedData => "mod-data-corrupted-" + (client ? "client" : "server"),
			_ => throw new InvalidOperationException($"Invalid status: {status} ({(int)status})"),
		};
	}
}
