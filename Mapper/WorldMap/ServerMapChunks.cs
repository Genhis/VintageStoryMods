namespace Mapper.WorldMap;

using Mapper.Util.IO;
using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using Vintagestory.API.MathTools;

public class ServerMapChunks : Dictionary<FastVec2i, ColorAndZoom> {
	public void FromBytesWithSizeLimit(byte[] data) {
		using MemoryStream stream = new(data);
		Span<byte> countBuffer = stackalloc byte[sizeof(int)];
		stream.ReadExactly(countBuffer);
		int count = BinaryPrimitives.ReadInt32LittleEndian(countBuffer);

		using VersionedReader input = VersionedReader.Create(stream, compressed: true);
		this.Clear();
		this.EnsureCapacity(Math.Min(count, SaveLoadExtensions.MaxInitialContainerSize));
		for(int i = 0; i < count; ++i)
			this[input.ReadFastVec2i()] = new ColorAndZoom(input);
	}

	public byte[]? ToBytesWithSizeLimit(int sizeLimit, MapChunks approvedChunks) {
		if(this.Count == 0)
			return null;

		using MemoryStream stream = new();
		Span<byte> countBuffer = stackalloc byte[sizeof(int)];
		stream.Write(countBuffer); // Reserve space for count.

		const int ItemSaveSize = sizeof(ulong) + sizeof(byte);
		const int ItemsInGroup = 100;
		int count = 0;
		using(VersionedWriter output = VersionedWriter.Create(stream, 4096, true, true))
			foreach(KeyValuePair<FastVec2i, ColorAndZoom> item in this) {
				if(count % ItemsInGroup == 0) {
					output.Flush();
					if(stream.Position + ItemSaveSize * ItemsInGroup > sizeLimit) {
						approvedChunks.Remove(item.Key);
						continue;
					}
				}

				output.Write(item.Key);
				item.Value.Save(output);
				++count;
			}

		stream.Position = 0;
		BinaryPrimitives.WriteInt32LittleEndian(countBuffer, count);
		stream.Write(countBuffer);
		return stream.ToArray();
	}
}
