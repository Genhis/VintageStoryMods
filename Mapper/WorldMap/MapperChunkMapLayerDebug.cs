#if DEBUG
namespace Mapper.WorldMap;

using Vintagestory.API.Common;

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

		public TextCommandResult Clear(TextCommandCallingArgs args) {
			if(this.mapper.serverStorage != null) {
				this.mapper.serverStorage[args.Caller.Player.PlayerUID].Regions.Clear();
			}
			else
				lock(this.mapper.clientStorage!.SaveLock) {
					this.mapper.clientStorage.Chunks.Clear();
					this.mapper.clientStorage.ChunksToRedraw.Clear();
				}
			return TextCommandResult.Success("Done!");
		}
	}
}
#endif
