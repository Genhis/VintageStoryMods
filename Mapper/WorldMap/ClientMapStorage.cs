namespace Mapper.WorldMap;

using System.Collections.Generic;
using Vintagestory.API.MathTools;

public class ClientMapStorage {
	public readonly Dictionary<FastVec2i, MapChunk> Chunks = [];
}
