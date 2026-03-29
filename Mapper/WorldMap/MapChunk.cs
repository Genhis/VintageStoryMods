namespace Mapper.WorldMap;

using Mapper.Util.IO;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;

public readonly struct MapChunk {
	public const int Size = GlobalConstants.ChunkSize;
	public const int Area = Size * Size;

	public readonly int[] Pixels;
	public readonly int Timestamp;
	public readonly ColorAndZoom ColorAndZoom;

	internal readonly int SaveSizeEstimate => sizeof(byte) + sizeof(int) + sizeof(int) * MapChunk.GetRequiredDurability(this.ColorAndZoom.ZoomLevel);

	public MapChunk(int[] pixels, int timestamp, ColorAndZoom colorAndZoom) {
		this.Pixels = pixels;
		this.Timestamp = timestamp;
		this.ColorAndZoom = colorAndZoom;
	}

	public MapChunk(VersionedReader input, FastVec2i chunkPosition, MapBackground background) {
		this.ColorAndZoom = new(input);
		if(this.ColorAndZoom.Color == 0) {
			this.Pixels = background.GetPixels(chunkPosition, this.ColorAndZoom.ZoomLevel);
			return;
		}

		if(input.InputVersion >= 2)
			this.Timestamp = input.ReadInt32();
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

		output.Write(this.Timestamp);
		int scaleFactor = 1 << this.ColorAndZoom.ZoomLevel;
		for(int y = 0; y < MapChunk.Size; y += scaleFactor) {
			int rowOffset = y * MapChunk.Size;
			for(int x = 0; x < MapChunk.Size; x += scaleFactor)
				output.Write(this.Pixels[rowOffset + x]);
		}
	}

	/// <returns>True if this chunk is better than the other.</returns>
	public readonly bool IsBetterThan(in MapChunk other, CartographyTableSyncModes modes) {
		if(modes.HasFlag(CartographyTableSyncModes.MoreRecent) && this.Timestamp != other.Timestamp)
			return this.Timestamp > other.Timestamp;
		if(modes.HasFlag(CartographyTableSyncModes.BetterResolution) && this.ColorAndZoom.ZoomLevel != other.ColorAndZoom.ZoomLevel)
			return this.ColorAndZoom.ZoomLevel < other.ColorAndZoom.ZoomLevel;
		if(modes.HasFlag(CartographyTableSyncModes.BetterColor) && this.ColorAndZoom.Color != other.ColorAndZoom.Color)
			return this.ColorAndZoom.Color > other.ColorAndZoom.Color;
		return false;
	}

	public static int GetAvailablePixels(int chunkCount, byte minZoomLevel) => chunkCount * MapChunk.Area / (1 << (minZoomLevel * 2));
	public static int GetRequiredDurability(byte zoomLevel) => MapChunk.Area >> (zoomLevel * 2);
}
