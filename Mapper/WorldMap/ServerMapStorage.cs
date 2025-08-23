namespace Mapper.WorldMap;

using Mapper.Util.IO;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Vintagestory.API.Server;

public class ServerMapStorage : Dictionary<string, ServerPlayerMap> {
	public bool Load(ICoreServerAPI api) {
		try {
			byte[] data = api.WorldManager.SaveGame.GetData("mapper:mapregions");
			if(data == null)
				return true;

			using VersionedReader input = VersionedReader.Create(new MemoryStream(data, false));
			int count = input.ReadInt32();
			this.EnsureCapacity(Math.Min(count, SaveLoadExtensions.MaxInitialContainerSize));
			for(int i = 0; i < count; ++i)
				this[input.ReadString()] = new ServerPlayerMap(input);

			api.Logger.Notification($"[mapper] Loaded {this.Count} players having {this.Sum(item => item.Value.Regions.Count)} map regions total");
			return true;
		}
		catch(Exception ex) {
			api.Logger.Error("[mapper] Failed to load map regions: " + ex.ToString());
			this.Clear();
			return false;
		}
	}

	public bool Save(ICoreServerAPI api) {
		try {
			using MemoryStream stream = new();
			using(VersionedWriter output = VersionedWriter.Create(stream, leaveOpen: true)) {
				output.Write(this.Count);
				foreach(KeyValuePair<string, ServerPlayerMap> item in this) {
					output.Write(item.Key);
					item.Value.Save(output);
				}
			}
			api.WorldManager.SaveGame.StoreData("mapper:mapregions", stream.ToArray());
			return true;
		}
		catch(Exception ex) {
			api.Logger.Error("[mapper] Failed to save map regions:" + ex.ToString());
			return false;
		}
	}
}
