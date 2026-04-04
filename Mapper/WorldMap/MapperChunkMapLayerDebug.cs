#if DEBUG
namespace Mapper.WorldMap;

using System.Collections.Concurrent;
using System.Collections.Generic;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;

public partial class MapperChunkMapLayer {
	private void RegisterDebugCommands() {
		DebugCommands debug = new(this);
		IChatCommand command = this.api.ChatCommands.GetOrCreate("mapper");
		command.BeginSubCommand("clear").RequiresPlayer().HandleWith(debug.Clear);
	}

	private class DebugCommands {
		private readonly MapperChunkMapLayer mapper;

		public DebugCommands(MapperChunkMapLayer mapper) {
			this.mapper = mapper;
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
	}
}
#endif
