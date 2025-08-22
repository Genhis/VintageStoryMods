namespace Mapper.WorldMap;

using Mapper.Util.IO;
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

	private static int GetIndex(FastVec2i chunkPosition) {
		return chunkPosition.Y % Size * Size + chunkPosition.X % Size;
	}
}
