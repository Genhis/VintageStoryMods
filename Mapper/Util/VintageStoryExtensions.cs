namespace Mapper.Util;

using Cairo;
using System;
using System.Collections.Generic;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
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

	public static SkillItem GetOrCreateWithNumber(this List<SkillItem?> cache, ICoreClientAPI api, int index, string code, int number, int affectedCount, AssetLocation iconPath) {
		return cache[index] ?? (cache[index] = new SkillItem() {
			Code = $"{code}-{number}",
			Name = Lang.Get(code, number, affectedCount),
		}.WithIcon(api, (ctx, x, y, width, height, rgba) => {
			api.Gui.DrawSvg(api.Assets.Get(iconPath), (ImageSurface)ctx.GetTarget(), x - 1, y - 1, (int)width + 2, (int)height + 2, null);

			ctx.SetSourceRGBA(rgba);
			ctx.SetFontSize((height + y * 2) * (2/3.0));
			ctx.SelectFontFace("Open Sans", FontSlant.Normal, FontWeight.Bold);
			string text = number.ToString();
			TextExtents extents = ctx.TextExtents(text);
			ctx.MoveTo(x + (width - extents.Width) / 2 - extents.XBearing, y + (height - extents.Height) / 2 - extents.YBearing);
			ctx.ShowText(text);
		}));
	}
}
