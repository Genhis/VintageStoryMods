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

	// ColorAndZoom is greater than or "better" if
	// 1. Zoom level is lower/resolution is higher
	// 2. Zoom level is the same but color level is higher
	// This means that resolution takes precedence over color level.
	// i.e. higher resolution B/W maps replaces lower resolution colored maps
	// - this was an intentional design decision
	public static bool operator >(ColorAndZoom l, ColorAndZoom r) {
		return l.ZoomLevel < r.ZoomLevel || l.ZoomLevel == r.ZoomLevel && l.Color > r.Color;
	}
	public static bool operator <(ColorAndZoom l, ColorAndZoom r) {
		return r > l;
	}
}
