namespace Mapper.GameContent;

using Mapper.Util;
using System;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;

public class InventoryCartographyTable : InventoryBase {
	private ItemSlot[] slots;

	public override int Count => 2;
	public ItemSlot MapSlot => this.slots[0];
	public ItemSlot PainsetSlot => this.slots[1];

	public InventoryCartographyTable() : base(null, null) {
		this.slots = this.GenEmptySlots(this.Count);
	}

	public override void FromTreeAttributes(ITreeAttribute tree) {
		this.slots = this.SlotsFromTreeAttributes(tree, this.slots);
	}

	public override void ToTreeAttributes(ITreeAttribute tree) {
		this.SlotsToTreeAttributes(this.slots, tree);
	}

	public override ItemSlot? this[int slotID] {
		get => slotID < 0 || slotID >= this.slots.Length ? null : this.slots[slotID];
		set {
			if(slotID < 0 || slotID >= this.slots.Length)
				throw new ArgumentOutOfRangeException(nameof(slotID));
			ArgumentNullException.ThrowIfNull(value);
			this.slots[slotID] = value;
		}
	}

	protected override ItemSlot NewSlot(int slotID) {
		return slotID switch {
			0 => new MapperItemSlot(this, "map"),
			1 => new MapperItemSlot(this, "paintset"),
			_ => throw new InvalidOperationException("Slot index not supported: " + slotID),
		};
	}
}
