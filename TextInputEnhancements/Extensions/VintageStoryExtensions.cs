namespace TextInputEnhancements.Extensions;

using Vintagestory.API.Client;

public static class VintageStoryExtensions {
	public static bool IsArrowKey(this KeyEvent e) {
		return e.KeyCode == (int)GlKeys.Up || e.KeyCode == (int)GlKeys.Down || e.KeyCode == (int)GlKeys.Left || e.KeyCode == (int)GlKeys.Right;
	}

	public static bool IsCursorMovementKey(this KeyEvent e) {
		return e.IsArrowKey() || e.KeyCode == (int)GlKeys.Home || e.KeyCode == (int)GlKeys.End;
	}

	public static bool IsDeleteCategoryKey(this KeyEvent e) {
		return e.KeyCode == (int)GlKeys.BackSpace || e.KeyCode == (int)GlKeys.Delete;
	}
}
