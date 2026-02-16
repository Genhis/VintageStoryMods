namespace Mapper.GameContent;

using Mapper.Util;
using Mapper.WorldMap;
using System;
using Vintagestory.API.Client;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;

public class GuiDialogBlockEntityCartographyTable : GuiDialogBlockEntity {
	private static CartographyTableSyncModes storedModes;
	private CartographyTableSyncModes modes; // Use instance variable in case the GUI is opened multiple times.

	public GuiDialogBlockEntityCartographyTable(BlockPos position, InventoryCartographyTable inventory, ICoreClientAPI api) : base(Lang.Get("mapper:gui-cartographytable-title"), inventory, position, api) {
		if(this.IsDuplicate)
			return;

		this.modes = GuiDialogBlockEntityCartographyTable.storedModes;
		this.SetupDialog();
	}

	public override void OnGuiClosed() {
		(this.SingleComposer.GetElement("mapSlot") as GuiElementItemSlotGridBase)?.OnGuiClosed(this.capi);
		(this.SingleComposer.GetElement("paintsetSlot") as GuiElementItemSlotGridBase)?.OnGuiClosed(this.capi);
		base.OnGuiClosed();
	}

	private void SetupDialog() {
		const double MaxWidth = 320;
		ElementBounds dialogBounds = ElementStdBounds.AutosizedMainDialog.WithAlignment(EnumDialogArea.CenterMiddle).WithFixedAlignmentOffset(-GuiStyle.DialogToScreenPadding, 0);
		ElementBounds backgroundBounds = ElementBounds.Fill.WithFixedPadding(GuiStyle.ElementToDialogPadding).WithSizing(ElementSizing.FitToChildren);
		ElementBounds bounds = ElementStdBounds.SlotGrid(EnumDialogArea.None, 0, 30, 1, 1);
		bounds.WithFixedX(MaxWidth / 2 - bounds.fixedWidth * 1.5);
		CairoFont switchFont = CairoFont.ButtonText().WithFontSize(18);

		Action<GuiComposer> AddCartographyModeSwitch(string langKey, CartographyTableSyncModes mode, ElementBounds bounds) {
			return composer => composer.AddSwitchWithText(Lang.Get(langKey), switchFont, state => {
				if(state)
					this.modes |= mode;
				else
					this.modes &= ~mode;
				GuiDialogBlockEntityCartographyTable.storedModes = this.modes;
			}, bounds, this.modes.HasFlag(mode));
		}

		this.ClearComposers();
		this.SingleComposer = this.capi.Gui
			.CreateCompo("blockentitycartographytable" + this.BlockEntityPosition, dialogBounds)
			.AddShadedDialogBG(backgroundBounds)
			.AddDialogTitleBar(this.DialogTitle, this.CloseIconPressed)
			.BeginChildElements(backgroundBounds)
				.AddItemSlotGrid(this.Inventory, this.SendInventoryPacket, 1, [0], bounds.Copy(), "mapSlot")
				.AddItemSlotGrid(this.Inventory, this.SendInventoryPacket, 1, [1], bounds.RightCopy(bounds.fixedWidth), "paintsetSlot")
				.Do(AddCartographyModeSwitch("mapper:gui-cartographytable-update-empty", CartographyTableSyncModes.EmptyChunks, bounds.WithFixedX(4).WithFixedSize(MaxWidth - 4, switchFont.GetFontExtents().Height).BelowCopyAndUpdate(0, 36)))
				.Do(AddCartographyModeSwitch("mapper:gui-cartographytable-update-time", CartographyTableSyncModes.MoreRecent, bounds.BelowCopyAndUpdate(0, 5)))
				.Do(AddCartographyModeSwitch("mapper:gui-cartographytable-update-resolution", CartographyTableSyncModes.BetterResolution, bounds.BelowCopyAndUpdate(0, 5)))
				.Do(AddCartographyModeSwitch("mapper:gui-cartographytable-update-color", CartographyTableSyncModes.BetterColor, bounds.BelowCopyAndUpdate(0, 5)))
				.AddButton(Lang.Get("mapper:gui-cartographytable-download"), () => this.RunAction(TransferDirection.Download), bounds.WithFixedX(0).WithFixedSize(MaxWidth, CairoFont.ButtonText().GetFontExtents().Height * 1.2).BelowCopyAndUpdate(0, -5))
				.AddButton(Lang.Get("mapper:gui-cartographytable-upload"), () => this.RunAction(TransferDirection.Upload), bounds.BelowCopyAndUpdate(0, 2))
			.EndChildElements()
			.Compose();
	}

	private void SendInventoryPacket(object packet) {
		this.capi.Network.SendBlockEntityPacket(this.BlockEntityPosition.X, this.BlockEntityPosition.Y, this.BlockEntityPosition.Z, packet);
	}

	private bool RunAction(TransferDirection transferDirection) {
		MapperChunkMapLayer mapLayer = MapperChunkMapLayer.GetInstance(this.capi);
		if(!mapLayer.CheckEnabledClient())
			return false;

		mapLayer.ScheduleCartographyTableSynchronization(this.BlockEntityPosition, this.modes, transferDirection);
		return true;
	}
}
