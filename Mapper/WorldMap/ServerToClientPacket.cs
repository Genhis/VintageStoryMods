namespace Mapper.WorldMap;

using ProtoBuf;
using System.Collections.Generic;
using Vintagestory.API.MathTools;

public enum ServerToClientPacketMode : byte {
	General,
	ApplyChunkColorMigration,
	ApplyPendingChanges,
	RecoverMap
}

[ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
public class ServerToClientPacket {
	public required ServerToClientPacketMode Mode;
	public Dictionary<FastVec2i, ColorAndZoom>? Changes;
	public List<FastVec2i>? Chunks;
	public Vec3d? LastKnownPosition;
	public int Time;
}
