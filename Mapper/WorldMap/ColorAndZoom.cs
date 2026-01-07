namespace Mapper.WorldMap;

using Mapper.Util.IO;
using ProtoBuf;

/// <summary>
/// A packed representation of color and zoom level variables as one byte.
/// </summary>
[ProtoContract]
public readonly struct ColorAndZoom {
	public const byte ZoomBits = 5;
	public const byte ZoomMask = (1 << ZoomBits) - 1;
	public const byte EmptyZoomLevel = ZoomMask;

	[ProtoMember(1)]
	public readonly byte Data = EmptyZoomLevel;

	public readonly bool Empty => this.Data == EmptyZoomLevel;
	public readonly byte Color => (byte)(this.Data >> ZoomBits);
	public readonly byte ZoomLevel => (byte)(this.Data & ZoomMask);

	public ColorAndZoom() {}

	public ColorAndZoom(byte color, byte zoomLevel) {
		this.Data = (byte)((color << ZoomBits) | (zoomLevel & ZoomMask));
	}

	public ColorAndZoom(VersionedReader input) {
		this.Data = input.ReadUInt8();
	}

	public readonly void Save(VersionedWriter output) {
		output.Write(this.Data);
	}
}
