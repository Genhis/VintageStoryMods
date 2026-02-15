namespace Mapper.Util;

using Cairo;
using System;
using System.Collections.Generic;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;

public static class VintageStoryExtensions {
	public static JsonObject GetMapperAttributes(this CollectibleObject obj) {
		return obj.Attributes?["mapper"] ?? new JsonObject(null);
	}

	public static int GetIntInRange(this JsonObject input, ILogger? logger, string key, int defaultValue, int from, int to) {
		int value = input[key].AsInt(defaultValue);
		if(value < from ||  value > to) {
			logger?.Warning($"Value {key} is out of allowed range ({from} to {to}), clamping it");
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

	public static void RegisterCustomIcon(this IconUtil iconUtil, ICoreClientAPI api, AssetLocation location) {
		System.Diagnostics.Debug.Assert(location.Path[location.Path.LastIndexOf('.')..] == ".svg");
		string iconName = location.Path[(location.Path.LastIndexOf('/') + 1)..location.Path.LastIndexOf('.')];
		iconUtil.CustomIcons[$"{location.Domain}:{iconName}"] = (ctx, x, y, width, height, rgba) => {
			api.Gui.DrawSvg(api.Assets.Get(location), (ImageSurface)ctx.GetTarget(), x, y, (int)width, (int)height, ColorUtil.FromRGBADoubles(rgba));
		};
	}

	public static int RegisterCustomIcons(this IconUtil iconUtil, ICoreClientAPI api, string pathBegins, string domain) {
		List<AssetLocation> locations = api.Assets.GetLocations(pathBegins, domain);
		foreach(AssetLocation location in locations)
			iconUtil.RegisterCustomIcon(api, location);
		return locations.Count;
	}

	public static ItemStack TakeOutAndMarkDirty(this ItemSlot slot, int quantity) {
		ItemStack result = slot.TakeOut(quantity);
		slot.MarkDirty();
		return result;
	}

	public static ItemStack TakeOutWholeAndMarkDirty(this ItemSlot slot) {
		ItemStack result = slot.TakeOutWhole();
		slot.MarkDirty();
		return result;
	}

	public static FastVec2i ToChunkPosition(this EntityPos entityPos) {
		return new FastVec2i((int)entityPos.X / GlobalConstants.ChunkSize, (int)entityPos.Z / GlobalConstants.ChunkSize);
	}

	public static FastVec2i ToChunkPosition(this Vec3d position) {
		return new FastVec2i((int)position.X / GlobalConstants.ChunkSize, (int)position.Z / GlobalConstants.ChunkSize);
	}
}
