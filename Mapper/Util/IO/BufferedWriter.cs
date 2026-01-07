namespace Mapper.Util.IO;

using System;
using System.Buffers.Binary;
using System.IO;
using System.Text;

public class BufferedWriter : IDisposable {
	private readonly Stream stream;
	private readonly byte[] buffer;
	private readonly bool leaveOpen;
	private int position;

	public BufferedWriter(Stream stream, int bufferSize, bool leaveOpen = false) {
		this.stream = stream;
		this.buffer = new byte[bufferSize];
		this.leaveOpen = leaveOpen;
	}

	public void Dispose() {
		this.Flush();
		if(!this.leaveOpen)
			this.stream.Dispose();
	}

	public void Flush() {
		this.stream.Write(this.buffer, 0, this.position);
		this.position = 0;
	}

	public void Write(byte value) {
		if(this.position == this.buffer.Length)
			this.Flush();
		this.buffer[this.position++] = value;
	}

	public void Write(bool value) => this.Write(value ? (byte)1 : (byte)0);
	public void Write(sbyte value) => this.Write((byte)value);
	public void Write(short value) => BinaryPrimitives.WriteInt16LittleEndian(this.RequestBuffer(sizeof(short)), value);
	public void Write(ushort value) => BinaryPrimitives.WriteUInt16LittleEndian(this.RequestBuffer(sizeof(ushort)), value);
	public void Write(int value) => BinaryPrimitives.WriteInt32LittleEndian(this.RequestBuffer(sizeof(int)), value);
	public void Write(uint value) => BinaryPrimitives.WriteUInt32LittleEndian(this.RequestBuffer(sizeof(uint)), value);
	public void Write(long value) => BinaryPrimitives.WriteInt64LittleEndian(this.RequestBuffer(sizeof(long)), value);
	public void Write(ulong value) => BinaryPrimitives.WriteUInt64LittleEndian(this.RequestBuffer(sizeof(ulong)), value);
	public void Write(float value) => BinaryPrimitives.WriteSingleLittleEndian(this.RequestBuffer(sizeof(float)), value);
	public void Write(double value) => BinaryPrimitives.WriteDoubleLittleEndian(this.RequestBuffer(sizeof(double)), value);

	public void Write(string value) {
		int estimatedByteCount = value.Length * 3 + sizeof(int);
		if(this.buffer.Length - this.position < estimatedByteCount) {
			this.Flush();
			if(this.buffer.Length < estimatedByteCount)
				throw new NotSupportedException("String is too large for the allocated buffer size");
		}

		Span<byte> buffer = this.buffer.AsSpan(this.position + sizeof(int), estimatedByteCount - sizeof(int));
		int actualByteCount = Encoding.UTF8.GetBytes(value, buffer);
		this.Write(actualByteCount);
		this.position += actualByteCount;
	}

	private Span<byte> RequestBuffer(int size) {
		if(this.position + size > this.buffer.Length)
			this.Flush();

		Span<byte> span = this.buffer.AsSpan(this.position, size);
		this.position += size;
		return span;
	}
}
