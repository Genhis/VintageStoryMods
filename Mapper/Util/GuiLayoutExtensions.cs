namespace Mapper.Util;

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
}
