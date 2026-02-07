namespace Mapper.WorldMap;

using ProtoBuf;
using System.Collections.Generic;
using Vintagestory.API.MathTools;

[ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
public class ClientToServerPacket {
	public required string PlayerUID;
	public ClientCartographyTableData? CartographyTableData;
	public Vec3d? LastKnownPosition;
	public bool RecoverMap;
}

[ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
public class ClientCartographyTableData {
	public required BlockPos Position;
	public byte[]? UploadedChunks; // MapChunks serialized using VersionedWriter. Sent as raw bytes because our binary serialization has better compression than ProtoBuf.
	public Dictionary<FastVec2i, ColorAndZoom>? RequestedChunks;
	public int BlockUpdateID;
}
