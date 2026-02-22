namespace Mapper.Util;

using System;
using System.Collections.Generic;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

public class CustomTextureSource : Dictionary<string, TextureAtlasPosition>, ITexPositionSource {
	public static int LoadedTextureCount { get; private set; }

	private readonly ITextureAtlasAPI atlas;
	private int textureCountInAtlas;

	public Size2i AtlasSize => this.atlas.Size;

	public CustomTextureSource(ITextureAtlasAPI atlas) {
		this.atlas = atlas;
		this.textureCountInAtlas = this.atlas.Positions.Length;
	}

	public CustomTextureSource(CustomTextureSource parent) : this(parent.atlas) {
		foreach(KeyValuePair<string, TextureAtlasPosition> item in parent)
			this[item.Key] = item.Value;
	}

	public void LoadTextures(CollectibleObject obj) {
		if(obj is Item item)
			this.LoadTextures(item.Textures);
		else if(obj is Block block)
			this.LoadTextures(block.Textures);
		else
			throw new NotSupportedException("Unsupported CollectibleObject type");
	}

	public void LoadTextures(IDictionary<string, CompositeTexture> textures) {
		foreach(KeyValuePair<string, CompositeTexture> item in textures) {
			this.atlas.GetOrInsertTexture(item.Value, out int _, out TextureAtlasPosition texturePosition);
			this.AddTexture(item.Key, texturePosition);
		}
	}

	public void LoadTextures(IDictionary<string, AssetLocation> textures) {
		foreach(KeyValuePair<string, AssetLocation> item in textures) {
			if(this.ContainsKey(item.Key))
				continue;
			this.atlas.GetOrInsertTexture(item.Value, out int _, out TextureAtlasPosition texturePosition);
			this.AddTexture(item.Key, texturePosition);
		}
	}

	private void AddTexture(string key, TextureAtlasPosition position) {
		this[key] = position;
		if(this.textureCountInAtlas != this.atlas.Positions.Length) {
			++CustomTextureSource.LoadedTextureCount;
			++this.textureCountInAtlas;
			System.Diagnostics.Debug.Assert(this.textureCountInAtlas == this.atlas.Positions.Length);
		}
	}
}
