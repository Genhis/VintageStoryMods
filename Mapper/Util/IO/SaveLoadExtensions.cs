namespace Mapper.Util.IO;

using Vintagestory.API.MathTools;

public static class SaveLoadExtensions {
	public const int DefaultBufferSize = 1024 * 64;
	public const int MaxInitialContainerSize = 1024;

	public static FastVec2i ReadFastVec2i(this VersionedReader input) {
		return new FastVec2i{val = input.ReadUInt64()};
	}

	public static void Write(this VersionedWriter output, FastVec2i value) {
		output.Write(value.val);
	}

	public static Vec3d ReadVec3d(this VersionedReader input) {
		return new Vec3d(input.ReadDouble(), input.ReadDouble(), input.ReadDouble());
	}

	public static void Write(this VersionedWriter output, Vec3d value) {
		output.Write(value.X);
		output.Write(value.Y);
		output.Write(value.Z);
	}

	public static Vec3d? ReadVec3dOptional(this VersionedReader input) {
		return input.ReadBoolean() ? input.ReadVec3d() : null;
	}

	public static void WriteOptional(this VersionedWriter output, Vec3d? value) {
		output.Write(value != null);
		if(value != null)
			output.Write(value);
	}
}
