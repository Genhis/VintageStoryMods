namespace Mapper.WorldMap;

using Mapper.Util;
using ProtoBuf;
using Vintagestory.API.MathTools;

[ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
public class ClientToServerPacket {
	public required string PlayerUID;
	public ClientCartographyTableData? CartographyTableData;
	public Vec3d? LastKnownPosition;
	public uint MigrationVersion;
	public bool RecoverMap;
}

[ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
public class ClientCartographyTableData {
	public required BlockPos Position;
	public required int BlockUpdateID;
	public required TransferDirection TransferDirection;
	// ServerMapChunks for Download or MapChunks for Upload, serialized using VersionedWriter.
	// Sent as raw bytes because our binary serialization has better compression than ProtoBuf.
	public byte[]? Data;
}
