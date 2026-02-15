namespace Mapper.WorldMap;

using Mapper.Util;
using System;
using Vintagestory.API.MathTools;

[Flags]
public enum CartographyTableSyncModes {
	None = 0,
	EmptyChunks = 1,
	BetterColor = 2,
	BetterResolution = 4,
}

public class CartographyTableSyncRequest(BlockPos position, CartographyTableSyncModes modes, TransferDirection transferDirection) {
	public readonly BlockPos Position = position;
	public readonly CartographyTableSyncModes Modes = modes;
	public readonly TransferDirection TransferDirection = transferDirection;
	public ClientCartographyTableData? PreparedPacket;
	public MapChunks? PendingChanges;
	public int IgnoredChunkCount;
	public int UploadedChunkCount;
	public bool Prepared;
}
