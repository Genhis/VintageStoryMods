namespace Mapper.WorldMap;

using Vintagestory.API.MathTools;
using Vintagestory.API.Util;

public readonly struct MapRegion {
	public const int Size = RegionPosition.RegionSize;
	public const int Area = Size * Size;

	private readonly ColorAndZoom[] data = new ColorAndZoom[Area];

	public MapRegion() {
		this.data.Fill(new ColorAndZoom());
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

	private static int GetIndex(FastVec2i chunkPosition) {
		return chunkPosition.Y % Size * Size + chunkPosition.X % Size;
	}
}
