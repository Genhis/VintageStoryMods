namespace Mapper.Util;

using System;
using Vintagestory.API.Common;

public class MapperItemSlot : ItemSlot {
	public readonly string Type;

	public MapperItemSlot(InventoryBase inventory, string type) : base(inventory) {
		this.Type = type;
		this.BackgroundIcon = "mapper:" + type;
	}

	public override bool CanHold(ItemSlot sourceSlot) {
		return this.MatchesType(sourceSlot.Itemstack) && base.CanHold(sourceSlot);
	}

	public override bool CanTakeFrom(ItemSlot sourceSlot, EnumMergePriority priority = EnumMergePriority.AutoMerge) {
		return this.MatchesType(sourceSlot.Itemstack) && base.CanTakeFrom(sourceSlot, priority);
	}

	private bool MatchesType(ItemStack? itemStack) {
		string? type = itemStack?.Collectible.GetMapperAttributes()["type"].AsString();
		return type != null && type.Equals(this.Type, StringComparison.InvariantCultureIgnoreCase);
	}
}
