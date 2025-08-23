namespace Mapper.WorldMap;

using ProtoBuf;
using System.Collections.Generic;
using Vintagestory.API.MathTools;

[ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
public class ServerToClientPacket {
	public Dictionary<FastVec2i, ColorAndZoom>? Changes;
	public Vec3d? LastKnownPosition;
	public bool RecoverMap;
}
