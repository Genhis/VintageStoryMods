namespace Mapper.WorldMap;

using Vintagestory.API.Config;

public readonly struct MapChunk {
	public const int Size = GlobalConstants.ChunkSize;
	public const int Area = Size * Size;
	public static readonly int[] UnexploredPixels = new int[Size * Size];

	public readonly int[] Pixels = MapChunk.UnexploredPixels;

	static MapChunk() {
		for(int y = 0, i = 0; y < Size; ++y)
			for(int x = 0; x < Size; ++x, ++i)
				MapChunk.UnexploredPixels[i] = (int)(x / 4 % 2 != y / 4 % 2 ? 0xFF98CCDC : 0xFF689AA8);
	}

	public MapChunk() {}
}
