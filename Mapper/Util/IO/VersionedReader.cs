namespace Mapper.Util.IO;

using System;
using System.Buffers.Binary;
using System.IO;
using System.IO.Compression;

public class VersionedReader : BufferedReader {
	public readonly uint InputVersion;

	protected VersionedReader(Stream stream, int bufferSize, bool leaveOpen, uint version) : base(stream, bufferSize, leaveOpen) {
		this.InputVersion = version;
	}

	public static VersionedReader Create(Stream stream, int bufferSize = SaveLoadExtensions.DefaultBufferSize, bool leaveOpen = false, bool compressed = false) {
		Span<byte> versionBuffer = stackalloc byte[sizeof(uint)];
		stream.ReadExactly(versionBuffer);
		uint version = BinaryPrimitives.ReadUInt32LittleEndian(versionBuffer);

		if(compressed) {
			stream = new DeflateStream(stream, CompressionMode.Decompress, leaveOpen);
			leaveOpen = false;
		}
		return new VersionedReader(stream, bufferSize, leaveOpen, version);
	}
}
