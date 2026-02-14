namespace Mapper.WorldMap;

using Vintagestory.API.MathTools;

public class CartographyTableSyncRequest(BlockPos position) {
	public readonly BlockPos Position = position;
	public ClientCartographyTableData? PreparedPacket;
	public MapChunks? PendingChanges;
	public int UploadedChunkCount;
	public bool Prepared;
}
