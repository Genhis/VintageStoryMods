namespace Mapper.WorldMap;

using Mapper.Util;
using Mapper.Util.IO;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

public class ClientMapStorage : IDisposable {
	public readonly Dictionary<FastVec2i, MapChunk> Chunks = [];
	public readonly DictionaryQueue<FastVec2i, ColorAndZoom> ChunksToRedraw = new();
	public readonly ReaderWriterLockSlim SaveLock = new();

	public void Dispose() {
		this.SaveLock.Dispose();
	}

	public bool Load(string filename, ILogger logger, MapBackground background) {
		if(!File.Exists(filename))
			return true;

		try {
			using VersionedReader input = VersionedReader.Create(new FileStream(filename, FileMode.Open, FileAccess.Read, FileShare.Read, SaveLoadExtensions.DefaultBufferSize, FileOptions.SequentialScan), compressed: true);
			this.Load(input, background);
			logger.Notification($"[mapper] Loaded {this.Chunks.Count} chunks out of which {this.ChunksToRedraw.Count} are waiting for refresh");
			return true;
		}
		catch(Exception ex) {
			logger.Error("[mapper] Failed to load client map storage: " + ex.ToString());
			this.Chunks.Clear();
			this.ChunksToRedraw.Clear();
			return false;
		}
	}

	public void Load(VersionedReader input, MapBackground background) {
		int count = input.ReadInt32();
		this.Chunks.EnsureCapacity(Math.Min(count, SaveLoadExtensions.MaxInitialContainerSize));
		for(int i = 0; i < count; ++i) {
			FastVec2i chunkPosition = input.ReadFastVec2i();
			this.Chunks[chunkPosition] = new MapChunk(input, chunkPosition, background);
		}

		count = input.ReadInt32();
		this.ChunksToRedraw.EnsureCapacity(Math.Min(count, SaveLoadExtensions.MaxInitialContainerSize));
		for(int i = 0; i < count; ++i)
			this.ChunksToRedraw.Enqueue(new KeyValuePair<FastVec2i, ColorAndZoom>(input.ReadFastVec2i(), new ColorAndZoom(input)));
	}

	public void Save(string filename, ILogger logger, ref bool dirtyFlag) {
		try {
			using VersionedWriter output = VersionedWriter.Create(new FileStream(filename, FileMode.Create, FileAccess.Write, FileShare.None, SaveLoadExtensions.DefaultBufferSize), compressed: true);
			this.Save(output, ref dirtyFlag);
		}
		catch(Exception ex) {
			logger.Error("[mapper] Failed to save client map storage: " + ex.ToString());
		}
	}

	public void Save(VersionedWriter output, ref bool dirtyFlag) {
		using IDisposable guard = this.SaveLock.ExclusiveLock();
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
		dirtyFlag = false;
	}
}
