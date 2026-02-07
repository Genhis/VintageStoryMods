namespace Mapper.WorldMap;

using Mapper.Util.IO;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;

public readonly struct MapChunk {
	public const int Size = GlobalConstants.ChunkSize;
	public const int Area = Size * Size;

	public readonly int[] Pixels;
	// TODO: this will need a migration for retrieving color from the server because clients didn't use to store it
	public readonly ColorAndZoom ColorAndZoom;

	public MapChunk(int[] pixels, ColorAndZoom colorAndZoom) {
		this.Pixels = pixels;
		this.ColorAndZoom = colorAndZoom;
	}

	public MapChunk(VersionedReader input, FastVec2i chunkPosition, MapBackground background) {
		this.ColorAndZoom = new(input);
		if(this.ColorAndZoom.Color == 0) {
			this.Pixels = background.GetPixels(chunkPosition, this.ColorAndZoom.ZoomLevel);
			return;
		}

		this.Pixels = new int[MapChunk.Area];
		int scaleFactor = 1 << this.ColorAndZoom.ZoomLevel;
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
		this.ColorAndZoom.Save(output);
		if(this.ColorAndZoom.Color == 0)
			return;

		int scaleFactor = 1 << this.ColorAndZoom.ZoomLevel;
		for(int y = 0; y < MapChunk.Size; y += scaleFactor) {
			int rowOffset = y * MapChunk.Size;
			for(int x = 0; x < MapChunk.Size; x += scaleFactor)
				output.Write(this.Pixels[rowOffset + x]);
		}
	}

	public static bool operator <(MapChunk l, MapChunk r) {
		return l.ColorAndZoom < r.ColorAndZoom;
	}
	public static bool operator >(MapChunk l, MapChunk r) => r < l;
}
