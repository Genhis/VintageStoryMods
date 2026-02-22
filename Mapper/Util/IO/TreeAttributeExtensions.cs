namespace Mapper.Util.IO;

using System;
using Vintagestory.API.Datastructures;

public static class TreeAttributeExtensions {
	private const int BytesPartSizeLimit = short.MaxValue; // https://github.com/anegostudios/vsapi/blob/c8fe81851e5c5400dc7a386fb58c7181532afb29/Datastructures/AttributeTree/ByteArrayAttribute.cs#L35

	public static bool HasLargeAttribute(this ITreeAttribute tree, string key) {
		return tree.HasAttribute(key) || tree.HasAttribute(key + "_parts");
	}

	public static byte[]? GetBytesLarge(this ITreeAttribute tree, string key) {
		if(tree.HasAttribute(key))
			return tree.GetBytes(key);

		int? numParts = tree.TryGetInt(key + "_parts");
		if(numParts == null)
			return null;

		int totalLength = tree.GetInt(key + "_totalLength");
		if(totalLength <= 0 || numParts <= 0)
			throw new InvalidOperationException("Large bytes array is corrupted");

		byte[] result = new byte[totalLength];
		int offset = 0;
		for(int i = 0; i < numParts; ++i) {
			byte[]? part = tree.GetBytes($"{key}_{i}");
			if(part == null || offset + part.Length > totalLength)
				throw new InvalidOperationException("Large bytes array is corrupted");

			Array.Copy(part, 0, result, offset, part.Length);
			offset += part.Length;
		}
		return result;
	}

	/// <returns>The number of parts the value was split into.</returns>
	public static int SetBytesLarge(this ITreeAttribute tree, string key, byte[] value) {
		if(value.Length <= BytesPartSizeLimit) {
			tree.SetBytes(key, value);
			return 1;
		}

		System.Diagnostics.Debug.Assert(!tree.HasAttribute(key));
		int numParts = MathUtil.CeiledDiv(value.Length, BytesPartSizeLimit);
		tree.SetInt(key + "_parts", numParts);
		tree.SetInt(key + "_totalLength", value.Length);

		for(int i = 0; i < numParts; ++i) {
			int offset = i * BytesPartSizeLimit;
			byte[] part = new byte[Math.Min(value.Length - offset, BytesPartSizeLimit)];
			Array.Copy(value, offset, part, 0, part.Length);
			tree.SetBytes($"{key}_{i}", part);
		}
		return numParts;
	}
}
