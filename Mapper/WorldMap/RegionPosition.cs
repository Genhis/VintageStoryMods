namespace Mapper.WorldMap;

using System;
using Vintagestory.API.MathTools;

public readonly struct RegionPosition : IEquatable<RegionPosition> {
	public const int RegionSize = 32;

	private readonly FastVec2i pos;

	public readonly int X => this.pos.X;
	public readonly int Y => this.pos.Y;

	private RegionPosition(FastVec2i pos) {
		this.pos = pos;
	}

	public readonly bool Equals(RegionPosition other) {
		return this.pos == other.pos;
	}

	public override readonly bool Equals(object? obj) {
		return obj is RegionPosition position && this.Equals(position);
	}

	public override readonly int GetHashCode() {
		return this.pos.GetHashCode();
	}

	public static bool operator==(RegionPosition left, RegionPosition right) {
		return left.Equals(right);
	}

	public static bool operator!=(RegionPosition left, RegionPosition right) {
		return !left.Equals(right);
	}

	public static RegionPosition FromChunkPosition(FastVec2i chunkPosition) {
		return new RegionPosition(new FastVec2i(chunkPosition.X / RegionSize, chunkPosition.Y / RegionSize));
	}
}
