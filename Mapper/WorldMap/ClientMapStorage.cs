namespace Mapper.WorldMap;

using Mapper.Util;
using System.Collections.Generic;
using Vintagestory.API.MathTools;

public class ClientMapStorage {
	public readonly Dictionary<FastVec2i, MapChunk> Chunks = [];
	public readonly DictionaryQueue<FastVec2i, ColorAndZoom> ChunksToRedraw = new();
}
