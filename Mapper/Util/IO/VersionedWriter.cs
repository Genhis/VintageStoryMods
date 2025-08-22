namespace Mapper.Util.IO;

using System;
using System.Buffers.Binary;
using System.IO;
using System.IO.Compression;

public class VersionedWriter : BufferedWriter {
	public const uint OutputVersion = 0;

	protected VersionedWriter(Stream stream, int bufferSize, bool leaveOpen) : base(stream, bufferSize, leaveOpen) {}

	public static VersionedWriter Create(Stream stream, int bufferSize = SaveLoadExtensions.DefaultBufferSize, bool leaveOpen = false, bool compressed = false) {
		Span<byte> versionBuffer = stackalloc byte[sizeof(uint)];
		BinaryPrimitives.WriteUInt32LittleEndian(versionBuffer, VersionedWriter.OutputVersion);
		stream.Write(versionBuffer);

		if(compressed) {
			stream = new DeflateStream(stream, CompressionMode.Compress, leaveOpen);
			leaveOpen = false;
		}
		return new VersionedWriter(stream, bufferSize, leaveOpen);
	}
}
