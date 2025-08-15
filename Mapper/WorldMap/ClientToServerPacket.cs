namespace Mapper.WorldMap;

using ProtoBuf;
using Vintagestory.API.MathTools;

[ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
public class ClientToServerPacket {
	public required string PlayerUID;
	public Vec3d? LastKnownPosition;
}
