namespace Mapper.Util.IO;

using System;
using Mapper.WorldMap;
using System.Collections.Generic;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;

public static class SaveLoadExtensions {
	public const int DefaultBufferSize = 1024 * 64;
	public const int MaxInitialContainerSize = 1024;

	public static FastVec2i ReadFastVec2i(this VersionedReader input) {
		return new FastVec2i { val = input.ReadUInt64() };
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

	public static Waypoint ReadWaypoint(this VersionedReader input) {
		return new Waypoint {
			Guid = input.ReadString(),
			Title = input.ReadString(),
			Text = input.ReadString(),
			Icon = input.ReadString(),
			Color = input.ReadInt32(),
			Position = input.ReadVec3d(),
			Pinned = input.ReadBoolean(),
			ShowInWorld = input.ReadBoolean(),
			OwningPlayerUid = input.ReadString(),
			OwningPlayerGroupId = input.ReadInt32(),
			Temporary = input.ReadBoolean()
		};
	}

	public static void Write(this VersionedWriter output, Waypoint waypoint) {
		output.Write(waypoint.Guid ?? "");
		output.Write(waypoint.Title ?? "");
		output.Write(waypoint.Text ?? "");
		output.Write(waypoint.Icon ?? "circle");
		output.Write(waypoint.Color);
		output.Write(waypoint.Position ?? new Vec3d());
		output.Write(waypoint.Pinned);
		output.Write(waypoint.ShowInWorld);
		output.Write(waypoint.OwningPlayerUid ?? "");
		output.Write(waypoint.OwningPlayerGroupId);
		output.Write(waypoint.Temporary);
	}


	public static void ReadChunks(this VersionedReader input, Dictionary<FastVec2i, MapChunk> chunks) {
		int count = input.ReadInt32();
		chunks.EnsureCapacity(Math.Min(count, SaveLoadExtensions.MaxInitialContainerSize));
		for(int i = 0; i < count; ++i) {
			chunks[input.ReadFastVec2i()] = new MapChunk(input);
		}
	}

	public static void Write(this VersionedWriter output, Dictionary<FastVec2i, MapChunk> chunks) {
		output.Write(chunks.Count);
		foreach((FastVec2i position, MapChunk chunk) in chunks) {
			output.Write(position);
			chunk.Save(output);
		}
	}
}
