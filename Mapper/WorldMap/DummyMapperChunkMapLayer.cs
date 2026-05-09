namespace Mapper.WorldMap;

using Mapper.Util;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;

public class DummyMapperChunkMapLayer : IMapperChunkMapLayer {
	private readonly ICoreAPI api;

	public DummyMapperChunkMapLayer(ICoreAPI api) {
		this.api = api;
	}

	public int MarkChunksForRedraw(IServerPlayer player, FastVec2i chunkPosition, int radius, int durability, byte colorLevel, byte zoomLevel, bool forceOverdraw) => durability;
	public bool UpdateLastKnownPosition(Vec3d? position) => false;
	public int? GetScaleFactor(IClientPlayer? player, FastVec2i chunkPosition) => null;
	public int? GetScaleFactor(IServerPlayer player, FastVec2i chunkPosition) => null;
	public bool HasLastKnownPosition() => false;
	public Vec3d GetPlayerOrLastKnownPosition() => new();
	public void TrySendUnrevealedMapMessage(IServerPlayer player) {}
	public void ScheduleCartographyTableSynchronization(BlockPos position, CartographyTableSyncModes modes, TransferDirection transferDirection) {}

	public bool CheckEnabledClient() {
		((ICoreClientAPI)this.api).TriggerIngameError(this, "mapper-mod-conflict", Lang.Get("mapper:error-mod-conflict"));
		return false;
	}

	public bool CheckEnabledServer(IServerPlayer player) {
		player.SendMessage(GlobalConstants.InfoLogChatGroup, Lang.GetL(player.LanguageCode, "mapper:error-mod-conflict"), EnumChatType.Notification);
		return false;
	}
}
