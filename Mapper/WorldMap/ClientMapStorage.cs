namespace Mapper.WorldMap;

using Mapper.Util;
using Mapper.Util.IO;
using System;
using System.Collections.Generic;
using System.IO;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

public class ClientMapStorage {
	public readonly Dictionary<FastVec2i, MapChunk> Chunks = [];
	public readonly DictionaryQueue<FastVec2i, ColorAndZoom> ChunksToRedraw = new();

	public void Load(string filename, ILogger logger) {
		if(!File.Exists(filename))
			return;

		try {
			using VersionedReader input = VersionedReader.Create(new FileStream(filename, FileMode.Open, FileAccess.Read, FileShare.Read, SaveLoadExtensions.DefaultBufferSize, FileOptions.SequentialScan), compressed: true);
			this.Load(input);
			logger.Notification($"[mapper] Loaded {this.Chunks.Count} chunks out of which {this.ChunksToRedraw.Count} are waiting for refresh");
		}
		catch(Exception ex) {
			logger.Error("[mapper] Failed to load client map storage: " + ex.ToString());
			this.Chunks.Clear();
			this.ChunksToRedraw.Clear();
		}
	}

	public void Load(VersionedReader input) {
		int count = input.ReadInt32();
		this.Chunks.EnsureCapacity(Math.Min(count, SaveLoadExtensions.MaxInitialContainerSize));
		for(int i = 0; i < count; ++i)
			this.Chunks[input.ReadFastVec2i()] = new MapChunk(input);

		count = input.ReadInt32();
		this.ChunksToRedraw.EnsureCapacity(Math.Min(count, SaveLoadExtensions.MaxInitialContainerSize));
		for(int i = 0; i < count; ++i)
			this.ChunksToRedraw.Enqueue(new KeyValuePair<FastVec2i, ColorAndZoom>(input.ReadFastVec2i(), new ColorAndZoom(input)));
	}

	public void Save(string filename, ILogger logger) {
		try {
			using VersionedWriter output = VersionedWriter.Create(new FileStream(filename, FileMode.Create, FileAccess.Write, FileShare.None, SaveLoadExtensions.DefaultBufferSize), compressed: true);
			this.Save(output);
		}
		catch(Exception ex) {
			logger.Error("[mapper] Failed to save client map storage: " + ex.ToString());
		}
	}

	public void Save(VersionedWriter output) {
		output.Write(this.Chunks.Count);
		foreach(KeyValuePair<FastVec2i, MapChunk> item in this.Chunks) {
			output.Write(item.Key);
			item.Value.Save(output);
		}

		output.Write(this.ChunksToRedraw.Count);
		foreach(KeyValuePair<FastVec2i, ColorAndZoom> item in this.ChunksToRedraw) {
			output.Write(item.Key);
			item.Value.Save(output);
		}
	}
}
