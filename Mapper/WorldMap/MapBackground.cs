namespace Mapper.WorldMap;

using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

public class MapBackground {
	public const int MaxZoomLevels = 6;
	private readonly int[][][] pixels;
	private readonly int chunkCountX;
	private readonly int chunkCountY;

	public MapBackground(ICoreClientAPI api, ILogger logger, AssetLocation assetLocation) {
		using BitmapRef bitmap = api.Assets.Get(assetLocation).ToBitmap(api);
		int[] sourcePixels = bitmap.Pixels;
		int sourceWidth = bitmap.Width;
		this.chunkCountX = sourceWidth / MapChunk.Size;
		this.chunkCountY = bitmap.Height / MapChunk.Size;
		this.pixels = new int[MaxZoomLevels][][];

		// Crop the image if dimensions are not multiples of chunk size.
		int croppedWidth = this.chunkCountX * MapChunk.Size;
		int croppedHeight = this.chunkCountY * MapChunk.Size;

		int estimatedMemoryUsage = 0;
		for(int zoomLevel = 0, arraySize = this.chunkCountX * this.chunkCountY; zoomLevel < MaxZoomLevels; ++zoomLevel, arraySize <<= 2) {
			int scaleFactor = 1 << zoomLevel;
			int[][] zoomLevelPixels = new int[arraySize][];
			int chunkSize = MapChunk.Size / scaleFactor;
			for(int chunkOffsetY = 0, i = 0; chunkOffsetY < croppedHeight; chunkOffsetY += chunkSize)
				for(int chunkOffsetX = 0; chunkOffsetX < croppedWidth; chunkOffsetX += chunkSize, ++i) {
					int[] pixels = new int[MapChunk.Area];
					for(int y = 0; y < chunkSize; ++y) {
						int rowOffset = (chunkOffsetY + y) * sourceWidth + chunkOffsetX;
						int offsetY = y * scaleFactor;
						for(int x = 0; x < chunkSize; ++x) {
							int pixel = ColorUtil.ReverseColorBytes(sourcePixels[rowOffset + x]);
							int offsetX = x * scaleFactor;
							for(int innerY = 0; innerY < scaleFactor; ++innerY) {
								int innerRowOffset = (offsetY + innerY) * MapChunk.Size + offsetX;
								for(int innerX = 0; innerX < scaleFactor; ++innerX)
									pixels[innerRowOffset + innerX] = pixel;
							}
						}
					}
					zoomLevelPixels[i] = pixels;
					estimatedMemoryUsage += MapChunk.Area * sizeof(int);
				}
			this.pixels[zoomLevel] = zoomLevelPixels;
		}
		logger.Notification($"Estimated RAM usage of map background is {estimatedMemoryUsage / 1024.0 / 1024.0} MB");
	}

	public int[] GetPixels(FastVec2i chunkPosition, int zoomLevel) {
		int scaleFactor = 1 << zoomLevel;
		int chunkCountX = this.chunkCountX * scaleFactor;
		int chunkCountY = this.chunkCountY * scaleFactor;
		return this.pixels[zoomLevel][chunkPosition.Y % chunkCountY * chunkCountX + chunkPosition.X % chunkCountX];
	}
}
