namespace Mapper.WorldMap;

using Mapper.Util.IO;
using Vintagestory.API.Config;

public readonly struct MapChunk {
	public const int Size = GlobalConstants.ChunkSize;
	public const int Area = Size * Size;
	public static readonly int[] UnexploredPixels = new int[Size * Size];

	public readonly int[] Pixels = MapChunk.UnexploredPixels;
	public readonly byte ZoomLevel;

	static MapChunk() {
		for(int y = 0, i = 0; y < Size; ++y)
			for(int x = 0; x < Size; ++x, ++i)
				MapChunk.UnexploredPixels[i] = (int)(x / 4 % 2 != y / 4 % 2 ? 0xFF98CCDC : 0xFF689AA8);
	}

	public MapChunk() {}

	public MapChunk(int[] pixels, byte zoomLevel) {
		this.Pixels = pixels;
		this.ZoomLevel = zoomLevel;
	}

	public MapChunk(VersionedReader input) {
		ColorAndZoom modeAndZoom = new(input);
		this.ZoomLevel = modeAndZoom.ZoomLevel;
		if(modeAndZoom.Color == 0)
			return;

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
		ColorAndZoom modeAndZoom = new(this.Pixels == MapChunk.UnexploredPixels ? (byte)0 : (byte)1, this.ZoomLevel);
		modeAndZoom.Save(output);
		if(modeAndZoom.Color == 0)
			return;

		int scaleFactor = 1 << this.ZoomLevel;
		for(int y = 0; y < MapChunk.Size; y += scaleFactor) {
			int rowOffset = y * MapChunk.Size;
			for(int x = 0; x < MapChunk.Size; x += scaleFactor)
				output.Write(this.Pixels[rowOffset + x]);
		}
	}
}
