namespace Mapper.WorldMap;

using ProtoBuf;
using System.Collections.Generic;
using Vintagestory.API.MathTools;

[ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
public class ServerToClientPacket {
	public required Dictionary<FastVec2i, ColorAndZoom> Changes;
}
