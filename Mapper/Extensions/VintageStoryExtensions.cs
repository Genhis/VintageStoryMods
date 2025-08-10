namespace Mapper.Extensions;

using System;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;

public static class VintageStoryExtensions {
	public static JsonObject GetMapperAttributes(this CollectibleObject obj) {
		return obj.Attributes?["mapper"] ?? new JsonObject(null);
	}

	public static int GetIntInRange(this JsonObject input, ILogger logger, string key, int defaultValue, int from, int to) {
		int value = input[key].AsInt(defaultValue);
		if(value < from ||  value > to) {
			logger.Warning($"Value {key} is out of allowed range ({from} to {to}), clamping it");
			value = Math.Clamp(value, from, to);
		}
		return value;
	}
}
