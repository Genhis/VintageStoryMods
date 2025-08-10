namespace Mapper.Items;

using Mapper.Extensions;
using System.Text;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;

public class ItemMap : Item {
	private int chunkCount;
	private byte colorLevel;
	private int minZoomLevel;
	private int maxZoomLevel;
	private int availablePixels;

	public override void OnLoaded(ICoreAPI api) {
		base.OnLoaded(api);

		JsonObject input = this.GetMapperAttributes();
		ILogger logger = api.Logger;
		this.chunkCount = input.GetIntInRange(logger, "mapChunkCount", 0, 0, 1 << 16);
		this.colorLevel = (byte)input.GetIntInRange(logger, "colorLevel", 0, 0, 3);
		this.minZoomLevel = input.GetIntInRange(logger, "minZoomLevel", 1, 1, 6);
		this.maxZoomLevel = input.GetIntInRange(logger, "maxZoomLevel", this.minZoomLevel, this.minZoomLevel, 6);

		--this.minZoomLevel;
		--this.maxZoomLevel;
		this.availablePixels = this.chunkCount * 1024 / (1 << (this.minZoomLevel * 2));
	}

	public override void GetHeldItemInfo(ItemSlot slot, StringBuilder description, IWorldAccessor world, bool withDebugInfo) {
		base.GetHeldItemInfo(slot, description, world, withDebugInfo);

		StringBuilder chunkCount = new();
		StringBuilder scales = new();
		for(int i = this.minZoomLevel; i <= this.maxZoomLevel; ++i) {
			chunkCount.Append(this.availablePixels / (1024 >> (i * 2))).Append(", ");
			scales.Append("1:").Append(1 << i).Append(", ");
		}

		description.AppendLine();
		description.AppendLine(Lang.Get("mapper:iteminfo-map-scales", scales.Remove(scales.Length - 2, 2)));
		description.AppendLine(Lang.Get("mapper:iteminfo-map-chunk-coverage", chunkCount.Remove(chunkCount.Length - 2, 2)));
	}
}
