namespace Mapper.Util.IO;

using System;
using System.Buffers.Binary;
using System.IO;
using System.Text;

public class BufferedReader : IDisposable {
	private readonly Stream stream;
	private readonly byte[] buffer;
	private readonly bool leaveOpen;
	private int position;
	private int length;

	public BufferedReader(Stream stream, int bufferSize, bool leaveOpen = false) {
		this.stream = stream;
		this.buffer = new byte[bufferSize];
		this.leaveOpen = leaveOpen;
	}

	public void Dispose() {
		if(!this.leaveOpen)
			this.stream.Dispose();
	}

	public byte ReadUInt8() {
		if(this.position == this.length)
			this.FillBuffer();
		return this.buffer[this.position++];
	}

	public bool ReadBoolean() => this.ReadUInt8() != 0;
	public sbyte ReadInt8() => (sbyte)this.ReadUInt8();
	public short ReadInt16() => BinaryPrimitives.ReadInt16LittleEndian(this.InternalRead(sizeof(short)));
	public ushort ReadUInt16() => BinaryPrimitives.ReadUInt16LittleEndian(this.InternalRead(sizeof(ushort)));
	public int ReadInt32() => BinaryPrimitives.ReadInt32LittleEndian(this.InternalRead(sizeof(int)));
	public uint ReadUInt32() => BinaryPrimitives.ReadUInt32LittleEndian(this.InternalRead(sizeof(uint)));
	public long ReadInt64() => BinaryPrimitives.ReadInt64LittleEndian(this.InternalRead(sizeof(long)));
	public ulong ReadUInt64() => BinaryPrimitives.ReadUInt64LittleEndian(this.InternalRead(sizeof(ulong)));
	public double ReadFloat() => BinaryPrimitives.ReadSingleLittleEndian(this.InternalRead(sizeof(float)));
	public double ReadDouble() => BinaryPrimitives.ReadDoubleLittleEndian(this.InternalRead(sizeof(double)));

	public string ReadString() {
		int actualByteCount = this.ReadInt32();
		if(this.buffer.Length < actualByteCount)
			throw new NotSupportedException("String is too large for the allocated buffer size");
		return Encoding.UTF8.GetString(this.InternalRead(actualByteCount));
	}

	private ReadOnlySpan<byte> InternalRead(int size) {
		if(this.position + size > this.length)
			this.FillBuffer();

		ReadOnlySpan<byte> span = new(this.buffer, this.position, size);
		this.position += size;
		return span;
	}

	private void FillBuffer() {
		int bytesCarriedOver = this.length - this.position;
		if(bytesCarriedOver != 0)
			Buffer.BlockCopy(this.buffer, this.position, this.buffer, 0, bytesCarriedOver);

		int bytesRead = this.stream.Read(this.buffer, bytesCarriedOver, this.buffer.Length - bytesCarriedOver);
		if(bytesRead == 0)
			throw new InvalidOperationException("Stream doesn't have enough data");
		this.position = 0;
		this.length = bytesCarriedOver + bytesRead;
	}
}
