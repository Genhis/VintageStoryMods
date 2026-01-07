namespace Mapper.Util;

using System.Collections.Generic;
using Vintagestory.API.MathTools;

public static class Iterators {
	public static IEnumerable<FastVec2i> Circle(FastVec2i center, int radius) {
		yield return center;

		for(int r = 1; r <= radius; ++r) {
			int sideLen = r * 2;
			for(int i = 1; i <= sideLen; ++i) // Left side (bottom to top)
				yield return new FastVec2i(center.X - r, center.Y + r - i);
			for(int i = 1; i <= sideLen; ++i) // Top side (left to right)
				yield return new FastVec2i(center.X - r + i, center.Y - r);
			for(int i = 1; i <= sideLen; ++i) // Right side (top to bottom)
				yield return new FastVec2i(center.X + r, center.Y - r + i);
			for(int i = 1; i <= sideLen; ++i) // Bottom side (right to left)
				yield return new FastVec2i(center.X + r - i, center.Y + r);
		}
	}
}
