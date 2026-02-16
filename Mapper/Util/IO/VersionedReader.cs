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
		uint version = Math.Max(BinaryPrimitives.ReadUInt32LittleEndian(versionBuffer), 1); // Let's leave version 0 as an empty value
		if(version > VersionedWriter.OutputVersion)
			throw new InvalidDataException($"Input version {version} is newer than {VersionedWriter.OutputVersion}, please update the mod.");

		if(compressed) {
			stream = new DeflateStream(stream, CompressionMode.Decompress, leaveOpen);
			leaveOpen = false;
		}
		return new VersionedReader(stream, bufferSize, leaveOpen, version);
	}
}
