namespace Mapper.WorldMap;

using Mapper.Util.IO;
using System.Collections.Generic;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;

public readonly struct MapRegion {
	public const int Size = RegionPosition.RegionSize;
	public const int Area = Size * Size;

	private readonly ColorAndZoom[] data = new ColorAndZoom[Area];

	public MapRegion() {
		this.data.Fill(new ColorAndZoom());
	}

	public MapRegion(VersionedReader input) {
		for(int i = 0; i < Area; ++i)
			this.data[i] = new ColorAndZoom(input);
	}

	public readonly void Save(VersionedWriter output) {
		for(int i = 0; i < Area; ++i)
			this.data[i].Save(output);
	}

	public readonly void PrepareClientRecovery(Dictionary<FastVec2i, ColorAndZoom> output, RegionPosition regionPosition) {
		int chunkOffsetX = regionPosition.X * Size;
		int chunkOffsetY = regionPosition.Y * Size;
		for(int i = 0; i < Area; ++i)
			if(!this.data[i].Empty) {
				this.data[i] = new ColorAndZoom(0, this.data[i].ZoomLevel); // Reset color level because we don't want players to cheat by corrupting their storage on purpose.
				output[new FastVec2i(chunkOffsetX + i % Size, chunkOffsetY + i / Size)] = this.data[i];
			}
	}

	/// <returns>New zoom level if something was changed.</returns>
	public readonly byte? SetColorAndZoomLevels(FastVec2i chunkPosition, byte color, byte zoomLevel, bool forceOverdraw) {
		int index = MapRegion.GetIndex(chunkPosition);
		ColorAndZoom data = this.data[index];
		if(zoomLevel != ColorAndZoom.EmptyZoomLevel && data.ZoomLevel > zoomLevel) {
			this.data[index] = new ColorAndZoom(color, zoomLevel);
			return zoomLevel;
		}
		if(!data.Empty && (forceOverdraw || data.Color < color)) {
			this.data[index] = new ColorAndZoom(color, data.ZoomLevel);
			return data.ZoomLevel;
		}
		return null;
	}

	public readonly byte GetZoomLevel(FastVec2i chunkPosition) {
		return this.data[MapRegion.GetIndex(chunkPosition)].ZoomLevel;
	}

	public readonly Dictionary<FastVec2i, ColorAndZoom> MergeFrom(MapRegion sourceRegion, RegionPosition regionPosition) {
		int chunkOffsetX = regionPosition.X * Size;
		int chunkOffsetY = regionPosition.Y * Size;
		Dictionary<FastVec2i, ColorAndZoom>? changes = [];

		for(int i = 0; i < Area; ++i) {
			ColorAndZoom sourceData = sourceRegion.data[i];
			if(sourceData.Empty)
				continue;

			ColorAndZoom targetData = this.data[i];

			if(targetData.Empty || sourceData > targetData) {
				this.data[i] = sourceData;
				FastVec2i chunkPos = new FastVec2i(chunkOffsetX + i % Size, chunkOffsetY + i / Size);
				changes[chunkPos] = sourceData;
			}
		}
		return changes;
	}

	private static int GetIndex(FastVec2i chunkPosition) {
		return chunkPosition.Y % Size * Size + chunkPosition.X % Size;
	}
}
