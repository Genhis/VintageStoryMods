namespace Mapper.WorldMap;

using Mapper.Util;
using Vintagestory.API.Client;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;

public interface IMapperChunkMapLayer {
	int MarkChunksForRedraw(IServerPlayer player, FastVec2i chunkPosition, int radius, int durability, byte colorLevel, byte zoomLevel, bool forceOverdraw = false);
	bool UpdateLastKnownPosition(Vec3d? position);
	bool CheckEnabledClient();
	bool CheckEnabledServer(IServerPlayer player);
	int? GetScaleFactor(IClientPlayer? player, FastVec2i chunkPosition);
	int? GetScaleFactor(IServerPlayer player, FastVec2i chunkPosition);
	bool HasLastKnownPosition();
	Vec3d GetPlayerOrLastKnownPosition();
	void TrySendUnrevealedMapMessage(IServerPlayer player);
	void ScheduleCartographyTableSynchronization(BlockPos position, CartographyTableSyncModes modes, TransferDirection transferDirection);
}
