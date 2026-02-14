namespace Mapper.GameContent;

using Mapper.Util;
using Mapper.WorldMap;
using Vintagestory.API.Client;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;

public class GuiDialogBlockEntityCartographyTable : GuiDialogBlockEntity {
	public GuiDialogBlockEntityCartographyTable(BlockPos position, ICoreClientAPI api) : base(Lang.Get("mapper:gui-cartographytable-title"), position, api) {
		if(this.IsDuplicate)
			return;

		this.SetupDialog();
	}

	private void SetupDialog() {
		const double MaxWidth = 320;
		ElementBounds dialogBounds = ElementStdBounds.AutosizedMainDialog.WithAlignment(EnumDialogArea.CenterMiddle).WithFixedAlignmentOffset(-GuiStyle.DialogToScreenPadding, 0);
		ElementBounds backgroundBounds = ElementBounds.Fill.WithFixedPadding(GuiStyle.ElementToDialogPadding).WithSizing(ElementSizing.FitToChildren);
		ElementBounds bounds = new();

		this.ClearComposers();
		this.SingleComposer = this.capi.Gui
			.CreateCompo("blockentitycartographytable" + this.BlockEntityPosition, dialogBounds)
			.AddShadedDialogBG(backgroundBounds)
			.AddDialogTitleBar(this.DialogTitle, this.CloseIconPressed)
			.BeginChildElements(backgroundBounds)
				.AddButton(Lang.Get("mapper:gui-cartographytable-download"), () => this.RunAction(TransferDirection.Download), bounds.WithFixedX(0).WithFixedSize(MaxWidth, CairoFont.ButtonText().GetFontExtents().Height * 1.2).BelowCopyAndUpdate(0, -5))
				.AddButton(Lang.Get("mapper:gui-cartographytable-upload"), () => this.RunAction(TransferDirection.Upload), bounds.BelowCopyAndUpdate(0, 2))
			.EndChildElements()
			.Compose();
	}

	private bool RunAction(TransferDirection transferDirection) {
		MapperChunkMapLayer mapLayer = MapperChunkMapLayer.GetInstance(this.capi);
		if(!mapLayer.CheckEnabledClient())
			return false;

		mapLayer.ScheduleCartographyTableSynchronization(this.BlockEntityPosition, transferDirection);
		return true;
	}
}
