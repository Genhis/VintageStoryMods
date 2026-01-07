namespace Mapper.Util;

using System;

public static class MathUtil {
	public static int CeiledDiv(int a, int b) => (a + b - 1) / b;
	public static int? Min(int? x, int? y) => x != null && y != null ? Math.Min(x.Value, y.Value) : x ?? y;
}
