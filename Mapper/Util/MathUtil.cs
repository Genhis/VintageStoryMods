namespace Mapper.Util;

using System;
using System.Collections.Generic;
using Vintagestory.API.Client;

public static class MathUtil {
	public static readonly Dictionary<string, float[]> HorizontalBlockRotationMatrix = new(){
		{"north", new Matrixf().Values},
		{"east", new Matrixf().Translate(0.5f, 0, 0.5f).RotateY(MathF.PI * 1.5f).Translate(-0.5f, 0, -0.5f).Values},
		{"south", new Matrixf().Translate(0.5f, 0, 0.5f).RotateY(MathF.PI).Translate(-0.5f, 0, -0.5f).Values},
		{"west", new Matrixf().Translate(0.5f, 0, 0.5f).RotateY(MathF.PI * 0.5f).Translate(-0.5f, 0, -0.5f).Values}
	};

	public static int CeiledDiv(int a, int b) => (a + b - 1) / b;
	public static int? Min(int? x, int? y) => x != null && y != null ? Math.Min(x.Value, y.Value) : x ?? y;
}
