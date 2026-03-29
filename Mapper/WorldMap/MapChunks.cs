namespace Mapper.WorldMap;

using Mapper.Util.IO;
using System;
using System.Buffers.Binary;
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

	public void FromBytesWithSizeLimit(byte[] data, MapBackground background) {
		using MemoryStream stream = new(data);
		Span<byte> countBuffer = stackalloc byte[sizeof(int)];
		stream.ReadExactly(countBuffer);
		int count = BinaryPrimitives.ReadInt32LittleEndian(countBuffer);

		using VersionedReader input = VersionedReader.Create(stream, compressed: true);
		this.Clear();
		this.EnsureCapacity(Math.Min(count, SaveLoadExtensions.MaxInitialContainerSize));
		for(int i = 0; i < count; ++i) {
			FastVec2i chunkPosition = input.ReadFastVec2i();
			this[chunkPosition] = new MapChunk(input, chunkPosition, background);
		}
	}

	public byte[]? ToBytesWithSizeLimit(int sizeLimit, out int skippedChunks) {
		if(this.Count == 0) {
			skippedChunks = 0;
			return null;
		}

		using MemoryStream stream = new();
		Span<byte> countBuffer = stackalloc byte[sizeof(int)];
		stream.Write(countBuffer); // Reserve space for count.

		sizeLimit -= 8192;
		int count = 0;
		int size = 0;
		using(VersionedWriter output = VersionedWriter.Create(stream, 4096, true, true)) {
			foreach(KeyValuePair<FastVec2i, MapChunk> item in this) {
				if(stream.Position > sizeLimit) {
					if(size == 0)
						size += (int)stream.Position - sizeLimit;
					size += sizeof(ulong) + item.Value.SaveSizeEstimate;
					if(size > 8192)
						break;
				}

				output.Write(item.Key);
				item.Value.Save(output);
				++count;
				output.Flush();
			}
		}

		skippedChunks = this.Count - count;
		stream.Position = 0;
		BinaryPrimitives.WriteInt32LittleEndian(countBuffer, count);
		stream.Write(countBuffer);
		return stream.ToArray();
	}

	/// <returns>A dictionary of map chunks which are better than currently stored in this object.</returns>
	public MapChunks FindBetter(MapChunks other, CartographyTableSyncModes modes) {
		MapChunks result = [];
		foreach(KeyValuePair<FastVec2i, MapChunk> item in other) {
			MapChunk otherChunk = item.Value;
			if(otherChunk.ColorAndZoom.Color == 0)
				continue;
			if(!this.TryGetValue(item.Key, out MapChunk chunk)) {
				if(modes.HasFlag(CartographyTableSyncModes.EmptyChunks))
					result[item.Key] = otherChunk;
			}
			else if(otherChunk.IsBetterThan(chunk, modes))
				result[item.Key] = otherChunk;
		}
		return result;
	}

	public Dictionary<FastVec2i, ColorAndZoom> ConvertToServerFormat() {
		Dictionary<FastVec2i, ColorAndZoom> result = [];
		result.EnsureCapacity(this.Count);
		foreach(KeyValuePair<FastVec2i, MapChunk> item in this)
			result[item.Key] = item.Value.ColorAndZoom;
		return result;
	}
}
