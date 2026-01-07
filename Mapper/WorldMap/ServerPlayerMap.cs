namespace Mapper.WorldMap;

using Mapper.Util.IO;
using System;
using System.Collections.Generic;
using Vintagestory.API.MathTools;

public class ServerPlayerMap {
	public readonly Dictionary<RegionPosition, MapRegion> Regions = [];
	public Vec3d? LastKnownPosition;
	public long NextUnrevealedMapMessage; // intentionally not saved

	public ServerPlayerMap() {}

	public ServerPlayerMap(VersionedReader input) {
		int count = input.ReadInt32();
		this.Regions.EnsureCapacity(Math.Min(count, SaveLoadExtensions.MaxInitialContainerSize));
		for(int i = 0; i < count; ++i)
			this.Regions[new RegionPosition(input)] = new MapRegion(input);
		this.LastKnownPosition = input.ReadVec3dOptional();
	}

	public void Save(VersionedWriter output) {
		output.Write(this.Regions.Count);
		foreach(KeyValuePair<RegionPosition, MapRegion> item in this.Regions) {
			item.Key.Save(output);
			item.Value.Save(output);
		}
		output.WriteOptional(this.LastKnownPosition);
	}

	public Dictionary<FastVec2i, ColorAndZoom> PrepareClientRecovery() {
		Dictionary<FastVec2i, ColorAndZoom> result = [];
		foreach(KeyValuePair<RegionPosition, MapRegion> item in this.Regions)
			item.Value.PrepareClientRecovery(result, item.Key);
		return result;
	}
}
