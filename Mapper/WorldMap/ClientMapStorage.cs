namespace Mapper.WorldMap;

using Mapper.Util;
using Mapper.Util.IO;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;

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
			logger.Notification($"Loaded {this.Chunks.Count} chunks out of which {this.ChunksToRedraw.Count} are waiting for refresh");
			return true;
		}
		catch(Exception ex) {
			logger.Error("Failed to load client map storage: " + ex.ToString());
			this.Chunks.Clear();
			this.ChunksToRedraw.Clear();
			return false;
		}
	}

	public void Load(VersionedReader input, MapBackground? background) {
		int count = input.ReadInt32();
		this.Chunks.EnsureCapacity(Math.Min(count, SaveLoadExtensions.MaxInitialContainerSize));
		for(int i = 0; i < count; ++i) {
			FastVec2i chunkPosition = input.ReadFastVec2i();
			this.Chunks[chunkPosition] = new MapChunk(input);
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
			logger.Error("Failed to save client map storage: " + ex.ToString());
		}
	}

	public void Save(VersionedWriter output, ref bool dirtyFlag) {
		using IDisposable guard = this.SaveLock.ExclusiveLock();
		output.Write(this.Chunks.Count);
		foreach((FastVec2i pos, MapChunk chunk) in this.Chunks) {
			output.Write(pos);
			chunk.Save(output);
		}

		output.Write(this.ChunksToRedraw.Count);
		foreach((FastVec2i pos, ColorAndZoom chunk) in this.ChunksToRedraw) {
			output.Write(pos);
			chunk.Save(output);
		}
		dirtyFlag = false;
	}

	public byte[] Save(ref bool dirtyFlag) {
		using MemoryStream tableData = new();
		using(VersionedWriter output = VersionedWriter.Create(tableData, leaveOpen: true, compressed: true)) {
			this.Save(output, ref dirtyFlag);
		}
		return tableData.ToArray();
	}
}
