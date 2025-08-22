namespace Mapper.Util.IO;

using System;
using System.Collections.Generic;
using System.IO;
using Vintagestory.API.MathTools;

public static class SaveLoadTests {
	public static void Run() {
		const int BufferSize = 64;

		// Serialization of Vintage Story classes could use constructors or internal variables.
		// Test it here to make sure their meaning doesn't change in the future. This should match SaveLoadExtensions.cs.
		FastVec2i fastVec2i = new(7, -53);
		Vec3d vec3d = new(17, -31, 297);

		using MemoryStream stream = new();
		using(VersionedWriter output = VersionedWriter.Create(stream, BufferSize, true)) {
			output.Write(fastVec2i);
			output.Write(vec3d);
		}

		stream.Position = 0;
		using(VersionedReader input = VersionedReader.Create(stream, BufferSize, true)) {
			CheckEquals(fastVec2i, input.ReadFastVec2i());
			CheckEquals(vec3d, input.ReadVec3d());
		}
	}

	private static void CheckEquals<T>(T expected, T actual) {
		if(!EqualityComparer<T>.Default.Equals(expected, actual))
			throw new InvalidOperationException($"Type {typeof(T).Name} failed save/load consistency");
	}
}
