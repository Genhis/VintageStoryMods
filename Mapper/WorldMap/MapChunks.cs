namespace Mapper.WorldMap;

using Mapper.Util.IO;
using System;
using System.Collections.Generic;
using System.IO;
using Vintagestory.API.MathTools;

public class MapChunks : Dictionary<FastVec2i, MapChunk> {
	public void Load(VersionedReader input, MapBackground background) {
		this.Clear();
		int count = input.ReadInt32();
		this.EnsureCapacity(Math.Min(count, SaveLoadExtensions.MaxInitialContainerSize));
		for(int i = 0; i < count; ++i) {
			FastVec2i chunkPosition = input.ReadFastVec2i();
			this[chunkPosition] = new MapChunk(input, chunkPosition, background);
		}
	}

	public void Save(VersionedWriter output) {
		output.Write(this.Count);
		foreach(KeyValuePair<FastVec2i, MapChunk> item in this) {
			output.Write(item.Key);
			item.Value.Save(output);
		}
	}

	public void FromBytes(byte[] data, MapBackground background) {
		using VersionedReader output = VersionedReader.Create(new MemoryStream(data), compressed: true);
		this.Load(output, background);
	}

	public byte[]? ToBytes() {
		if(this.Count == 0)
			return null;

		using MemoryStream stream = new();
		using(VersionedWriter output = VersionedWriter.Create(stream, leaveOpen: true, compressed: true))
			this.Save(output);
		return stream.ToArray();
	}

	/// <returns>A dictionary of map chunks which are better than currently stored in this object.</returns>
	public MapChunks FindBetter(MapChunks other) {
		MapChunks result = [];
		foreach(KeyValuePair<FastVec2i, MapChunk> item in other)
			if(item.Value.ColorAndZoom.Color != 0 && (!this.TryGetValue(item.Key, out MapChunk chunk) || chunk < item.Value))
				result[item.Key] = item.Value;
		return result;
	}
}
