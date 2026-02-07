namespace Mapper.WorldMap;

using Mapper.Util;
using Mapper.Util.IO;
using System;
using System.Collections.Generic;
using System.IO;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

public class ClientMapStorage {
	public readonly MapChunks Chunks = [];
	public readonly DictionaryQueue<FastVec2i, ColorAndZoom> ChunksToRedraw = new();

	/// <summary>
	/// Always use this lock when writing to client storage.<br/>
	/// When reading, use it only if you care about data integrity as a whole, not for accessing individual chunks.<br/>
	/// Also use it when you iterate over the stored objects.
	/// </summary>
	public readonly object SaveLock = new();

	public bool Load(string filename, ILogger logger, MapBackground background) {
		if(!File.Exists(filename))
			return true;

		try {
			using VersionedReader input = VersionedReader.Create(new FileStream(filename, FileMode.Open, FileAccess.Read, FileShare.Read, SaveLoadExtensions.DefaultBufferSize, FileOptions.SequentialScan), compressed: true);
			lock(this.SaveLock)
				this.LoadInternal(input, background);
			logger.Notification($"Loaded {this.Chunks.Count} chunks out of which {this.ChunksToRedraw.Count} are waiting for refresh");
			return true;
		}
		catch(Exception ex) {
			logger.Error("Failed to load client map storage: " + ex.ToString());
			lock(this.SaveLock) {
				this.Chunks.Clear();
				this.ChunksToRedraw.Clear();
			}
			return false;
		}
	}

	private void LoadInternal(VersionedReader input, MapBackground background) {
		this.Chunks.Load(input, background);

		int count = input.ReadInt32();
		this.ChunksToRedraw.EnsureCapacity(Math.Min(count, SaveLoadExtensions.MaxInitialContainerSize));
		for(int i = 0; i < count; ++i)
			this.ChunksToRedraw.Enqueue(new KeyValuePair<FastVec2i, ColorAndZoom>(input.ReadFastVec2i(), new ColorAndZoom(input)));
	}

	public void Save(string filename, ILogger logger, ref bool dirtyFlag) {
		try {
			using VersionedWriter output = VersionedWriter.Create(new FileStream(filename, FileMode.Create, FileAccess.Write, FileShare.None, SaveLoadExtensions.DefaultBufferSize), compressed: true);
			lock(this.SaveLock) {
				this.SaveInternal(output);
				dirtyFlag = false;
			}
		}
		catch(Exception ex) {
			logger.Error("Failed to save client map storage: " + ex.ToString());
		}
	}

	private void SaveInternal(VersionedWriter output) {
		this.Chunks.Save(output);

		output.Write(this.ChunksToRedraw.Count);
		foreach(KeyValuePair<FastVec2i, ColorAndZoom> item in this.ChunksToRedraw) {
			output.Write(item.Key);
			item.Value.Save(output);
		}
	}
}
