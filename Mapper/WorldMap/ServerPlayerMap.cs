namespace Mapper.WorldMap;

using System.Collections.Generic;
using Vintagestory.API.MathTools;

public class ServerPlayerMap {
	public readonly Dictionary<RegionPosition, MapRegion> Regions = [];
	public Vec3d? LastKnownPosition;
}
