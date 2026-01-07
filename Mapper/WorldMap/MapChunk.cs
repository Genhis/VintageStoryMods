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

	public MapChunk(int[] pixels, byte zoomLevel, bool unexplored) {
		this.Pixels = pixels;
		this.modeAndZoom = new ColorAndZoom(unexplored ? (byte)0 : (byte)1, zoomLevel);
	}

	public MapChunk(VersionedReader input, FastVec2i chunkPosition, MapBackground background) {
		this.modeAndZoom = new(input);
		if(this.modeAndZoom.Color == 0) {
			this.Pixels = background.GetPixels(chunkPosition, this.ZoomLevel);
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
}
