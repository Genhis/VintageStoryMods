namespace Mapper.Util;

using Cairo;
using System;
using Vintagestory.API.Client;

public static class GuiLayoutExtensions {
	public static ElementBounds Copy(this ElementBounds bounds) {
		ElementBounds result = bounds.RightCopy(); // I didn't want to reimplement the whole copy function, this is good enough for now.
		result.fixedX = bounds.fixedX;
		return result;
	}

	public static ElementBounds BelowCopyAndUpdate(this ElementBounds bounds, double fixedDeltaX = 0, double fixedDeltaY = 0) {
		ElementBounds result = bounds.BelowCopy(fixedDeltaX, fixedDeltaY);
		bounds.fixedY = result.fixedY;
		return result;
	}

	public static ElementBounds WithFixedX(this ElementBounds bounds, double fixedX) {
		bounds.fixedX = fixedX;
		return bounds;
	}

	public static GuiComposer AddSwitchWithText(this GuiComposer composer, string text, CairoFont font, Action<bool> onToggle, ElementBounds bounds, bool state) {
		if(composer.Composed)
			return composer;

		FontExtents extents = font.GetFontExtents();
		double fixedDeltaX = extents.Descent * 1.5;
		double width = bounds.fixedWidth;
		bounds.fixedWidth = bounds.fixedHeight;
		return composer.AddInteractiveElement(new GuiElementSwitch(composer.Api, onToggle, bounds, bounds.fixedHeight) { On = state })
			.AddStaticText(text, font, EnumTextOrientation.Left, bounds.RightCopy(fixedDeltaX, -extents.Descent / 2).WithFixedWidth(width - bounds.fixedWidth - fixedDeltaX));
	}

	public static GuiComposer Do(this GuiComposer composer, Action<GuiComposer> action) {
		action(composer);
		return composer;
	}
}
