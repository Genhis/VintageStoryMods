namespace Mapper.WorldMap;

using Mapper.Util.IO;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;

public readonly struct MapChunk {
	public const int Size = GlobalConstants.ChunkSize;
	public const int Area = Size * Size;

	public readonly int[] Pixels;
	private readonly ColorAndZoom modeAndZoom;

	public readonly byte ZoomLevel => this.modeAndZoom.ZoomLevel;
	public readonly byte ColorLevel => this.modeAndZoom.Color;

	public MapChunk(int[] pixels, byte zoomLevel, byte colorLevel) {
		this.Pixels = pixels;
		this.modeAndZoom = new ColorAndZoom(colorLevel, zoomLevel);
	}

	public MapChunk(VersionedReader input, FastVec2i chunkPosition, MapBackground? background) {
		this.modeAndZoom = new(input);
		if(this.modeAndZoom.Color == 0) {
			// No pixel data was written for Color == 0 chunks
			this.Pixels = background?.GetPixels(chunkPosition, this.ZoomLevel) ?? new int[MapChunk.Area];
			return;
		}

		this.Pixels = new int[MapChunk.Area];
		int scaleFactor = 1 << this.ZoomLevel;
		for(int y = 0; y < MapChunk.Size; y += scaleFactor)
			for(int x = 0; x < MapChunk.Size; x += scaleFactor) {
				int pixel = input.ReadInt32();
				for(int innerY = 0; innerY < scaleFactor; ++innerY) {
					int rowOffset = (y + innerY) * MapChunk.Size + x;
					for(int innerX = 0; innerX < scaleFactor; ++innerX)
						this.Pixels[rowOffset + innerX] = pixel;
				}
			}
	}

	public readonly void Save(VersionedWriter output) {
		this.modeAndZoom.Save(output);
		if(this.modeAndZoom.Color == 0)
			return;

		int scaleFactor = 1 << this.ZoomLevel;
		for(int y = 0; y < MapChunk.Size; y += scaleFactor) {
			int rowOffset = y * MapChunk.Size;
			for(int x = 0; x < MapChunk.Size; x += scaleFactor)
				output.Write(this.Pixels[rowOffset + x]);
		}
	}

	// Chunk is greater than or "better" if
	// 1. Zoom level is lower/resolution is higher
	// 2. Zoom level is the same but color level is higher
	// This means that resolution takes precedence over color level.
	// i.e. higher resolution B/W maps replaces lower resolution colored maps
	// - this was an intentional design decision
	public static bool operator >(MapChunk l, MapChunk r) {
		return l.ZoomLevel < r.ZoomLevel || l.ZoomLevel == r.ZoomLevel && l.ColorLevel > r.ColorLevel;
	}
	public static bool operator <(MapChunk l, MapChunk r) {
		return r > l;
	}
}
