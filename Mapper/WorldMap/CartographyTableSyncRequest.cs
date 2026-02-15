namespace Mapper.WorldMap;

using Mapper.Util;
using Vintagestory.API.MathTools;

public class CartographyTableSyncRequest(BlockPos position, TransferDirection transferDirection) {
	public readonly BlockPos Position = position;
	public readonly TransferDirection TransferDirection = transferDirection;
	public ClientCartographyTableData? PreparedPacket;
	public MapChunks? PendingChanges;
	public int IgnoredChunkCount;
	public int UploadedChunkCount;
	public bool Prepared;
}
