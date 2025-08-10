namespace Mapper.Items;

using Mapper.Extensions;
using Mapper.Util;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;

public class ItemPaintbrush : Item {
	private bool hasUpgradeMode;
	private int minRange;
	private int stepRange;
	private int rangeCount;

	public override void OnLoaded(ICoreAPI api) {
		base.OnLoaded(api);

		JsonObject input = this.GetMapperAttributes();
		ILogger logger = api.Logger;
		this.hasUpgradeMode = input["upgradeMode"].AsBool(true);
		this.minRange = input.GetIntInRange(logger, "minRange", 0, 0, 20);
		int maxRange = input.GetIntInRange(logger, "maxRange", this.minRange, this.minRange, 99);
		this.stepRange = input.GetIntInRange(logger, "stepRange", 1, 1, 99);

		this.rangeCount = MathUtil.CeiledDiv(maxRange - this.minRange + 1, this.stepRange);
	}
}
