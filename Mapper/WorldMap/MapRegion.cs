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

	public readonly void MergeFrom(MapRegion source, Dictionary<FastVec2i, ColorAndZoom> changes, RegionPosition regionPosition) {
		int chunkOffsetX = regionPosition.X * Size;
		int chunkOffsetY = regionPosition.Y * Size;

		for(int i = 0; i < Area; ++i) {
			ColorAndZoom sourceData = source.data[i];
			if(sourceData.Empty)
				continue;

			ColorAndZoom targetData = this.data[i];

			// Update map if
			// 1. Map chunk doesn't exist
			// 2. Zoom level is lower/resolution is higher
			// 3. Zoom level is the same but color level is higher
			// This means that resolution takes precedence over color level.
			// i.e. higher resolution B/W maps replaces lower resolution colored maps
			// - this was an intentional design decision
			if(targetData.Empty ||
			   sourceData.ZoomLevel < targetData.ZoomLevel ||
			   sourceData.ZoomLevel == targetData.ZoomLevel && sourceData.Color > targetData.Color) {
				this.data[i] = sourceData;
				FastVec2i chunkPos = new FastVec2i(chunkOffsetX + i % Size, chunkOffsetY + i / Size);
				changes[chunkPos] = sourceData;
			}
		}
	}

	private static int GetIndex(FastVec2i chunkPosition) {
		return chunkPosition.Y % Size * Size + chunkPosition.X % Size;
	}
}
