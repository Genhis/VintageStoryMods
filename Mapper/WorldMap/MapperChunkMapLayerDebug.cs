#if DEBUG
namespace Mapper.WorldMap;

using Mapper.Util;
using ProtoBuf;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.API.Util;
using Vintagestory.GameContent;

[ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
public class ClientDebugData {
	public Dictionary<FastVec2i, ColorAndZoom>? ReplaceMap;
}

public partial class MapperChunkMapLayer {
	private void RegisterDebugCommands() {
		DebugCommands debug = new(this);
		CommandArgumentParsers parsers = this.api.ChatCommands.Parsers;
		IChatCommand command = this.api.ChatCommands.GetOrCreate("mapper");
		command.BeginSubCommand("clear").RequiresPlayer().HandleWith(debug.Clear);

		if(this.api.Side == EnumAppSide.Client)
			command.BeginSubCommand("clone").HandleWith(debug.Clone).EndSubCommand()
				.BeginSubCommand("generate").WithArgs(parsers.Int("radius"), parsers.IntRange("colorLevel", 0, 3), parsers.IntRange("zoomLevel", 1, 6)).HandleWith(debug.Generate);
	}

	private void OnDebugDataFromClient(IServerPlayer player, ClientDebugData data) {
		if(data.ReplaceMap != null) {
			Dictionary<RegionPosition, MapRegion> storedRegions = this.serverStorage![player.PlayerUID].Regions;
			storedRegions.Clear();
			foreach(KeyValuePair<FastVec2i, ColorAndZoom> item in data.ReplaceMap)
				storedRegions.GetOrCreate(RegionPosition.FromChunkPosition(item.Key)).ForceSetColorAndZoom(item.Key, item.Value);
			this.dirty = true;
			player.SendMessage(GlobalConstants.CurrentChatGroup, "Done!", EnumChatType.Notification);
		}
	}

	private class DebugCommands {
		private readonly MapperChunkMapLayer mapper;
		private readonly int[] vintageColors;
		private readonly uint oceanColor;

		public DebugCommands(MapperChunkMapLayer mapper) {
			this.mapper = mapper;

			this.vintageColors = new int[mapper.colorsByCode.Count];
			for(int i = 0; i < this.vintageColors.Length; ++i)
				this.vintageColors[i] = (int)((uint)mapper.colorsByCode.GetValueAtIndex(i) | 0xFF000000);
			this.oceanColor = (uint)mapper.colorsByCode.Get("ocean", 0) | 0xFF000000;
		}

		private void SendToServer(ClientDebugData data) {
			this.mapper.mapSink.SendMapDataToServer(this.mapper, SerializerUtil.Serialize(new ClientToServerPacket{PlayerUID = this.mapper.ClientPlayer.PlayerUID, DebugData = data}));
		}

		private void RedrawMap() {
			ConcurrentDictionary<FastVec2i, MultiChunkMapComponent> loadedMapData = MapperChunkMapLayer.loadedMapData.GetValue(this.mapper);
			foreach(MultiChunkMapComponent value in loadedMapData.Values)
				value.ActuallyDispose();
			loadedMapData.Clear();

			UniqueQueue<FastVec2i> chunksToGen = MapperChunkMapLayer.chunksToGen.GetValue(this.mapper);
			object chunksToGenLock = MapperChunkMapLayer.chunksToGenLock.GetValue(this.mapper);
			HashSet<FastVec2i> curVisibleChunks = MapperChunkMapLayer.curVisibleChunks.GetValue(this.mapper);
			lock(chunksToGenLock)
				foreach(FastVec2i curVisibleChunk in curVisibleChunks)
					chunksToGen.Enqueue(curVisibleChunk);
		}

		public TextCommandResult Clear(TextCommandCallingArgs args) {
			if(!this.mapper.Enabled)
				return TextCommandResult.Error("Error: Mod disabled");

			if(this.mapper.serverStorage != null) {
				this.mapper.serverStorage[args.Caller.Player.PlayerUID].Regions.Clear();
				this.mapper.dirty = true;
			}
			else {
				lock(this.mapper.clientStorage!.SaveLock) {
					this.mapper.clientStorage.Chunks.Clear();
					this.mapper.clientStorage.ChunksToRedraw.Clear();
					this.mapper.dirty = true;
				}
				this.RedrawMap();
			}
			return TextCommandResult.Success("Done!");
		}

		public TextCommandResult Clone(TextCommandCallingArgs args) {
			if(!this.mapper.Enabled)
				return TextCommandResult.Error("Error: Mod disabled");

			Dictionary<FastVec2i, ColorAndZoom> chunks = [];
			lock(this.mapper.clientStorage!.SaveLock)
				foreach(KeyValuePair<FastVec2i, MapChunk> item in this.mapper.clientStorage.Chunks)
					chunks[item.Key] = item.Value.ColorAndZoom;
			this.SendToServer(new ClientDebugData{ReplaceMap = chunks});
			return TextCommandResult.Success("Clone request sent!");
		}

		public TextCommandResult Generate(TextCommandCallingArgs args) {
			if(!this.mapper.Enabled)
				return TextCommandResult.Error("Error: Mod disabled");

			int radius = (int)args[0];
			byte colorLevel = (byte)(int)args[1];
			byte zoomLevel = (byte)((int)args[2] - 1);
			(int centerX, int centerY) = this.mapper.ClientPlayer.Entity.Pos.ToChunkPosition().ToTuple();
			ColorAndZoom colorAndZoom = new(colorLevel, zoomLevel);

			Random random = new();
			Dictionary<FastVec2i, ColorAndZoom> chunks = [];
			lock(this.mapper.clientStorage!.SaveLock) {
				this.mapper.clientStorage.Chunks.Clear();
				this.mapper.clientStorage.ChunksToRedraw.Clear();

				for(int y = -radius; y <= radius; ++y)
					for(int x = -radius; x <= radius; ++x) {
						FastVec2i position = new(centerX + x, centerY + y);
						int[] pixels = this.GeneratePixels(random, position, colorLevel, zoomLevel);
						this.mapper.clientStorage.Chunks[position] = new MapChunk(pixels, 0, colorAndZoom);
						chunks[position] = colorAndZoom;
					}
				this.mapper.dirty = true;
			}
			this.SendToServer(new ClientDebugData{ReplaceMap = chunks});
			this.RedrawMap();
			return TextCommandResult.Success("Map generated, clone request sent!");
		}

		private int[] GeneratePixels(Random random, in FastVec2i chunkPosition, byte colorLevel, byte zoomLevel) {
			if(colorLevel == 0)
				return this.mapper.background!.GetPixels(chunkPosition, zoomLevel);

			int[] pixels = new int[MapChunk.Area];
			if(colorLevel != 3) {
				for(int i = 0; i < MapChunk.Area; ++i)
					pixels[i] = this.vintageColors[random.Next() % this.vintageColors.Length];
				if(colorLevel == 1)
					MapperChunkMapLayer.ConvertToGrayscale(pixels, this.mapper.background!.GetPixels(chunkPosition, zoomLevel), this.oceanColor);
			}
			else
				for(int i = 0; i < MapChunk.Area; ++i)
					pixels[i] = (int)((uint)random.Next() | 0xFF000000u);

			if(zoomLevel != 0)
				MapperChunkMapLayer.ApplyBoxFilter(pixels, 1u << zoomLevel);
			return pixels;
		}
	}
}
#endif
