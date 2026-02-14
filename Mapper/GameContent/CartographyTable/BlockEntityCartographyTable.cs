namespace Mapper.GameContent;

using Mapper.Util.IO;
using Mapper.WorldMap;
using System;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;

public class BlockEntityCartographyTable : BlockEntity {
	public readonly MapChunks Chunks = [];
	internal int lastUpdateID;

	private GuiDialogBlockEntity? guiDialog;

	public override void FromTreeAttributes(ITreeAttribute tree, IWorldAccessor world) {
		base.FromTreeAttributes(tree, world);

		this.lastUpdateID = tree.GetInt("lastUpdateID");
		this.Chunks.Clear();
		byte[]? chunksData = tree.GetBytesLarge("chunks");
		try {
			if(chunksData != null)
				this.Chunks.FromBytes(chunksData, null!); // Cartography table doesn't store unexplored chunks, so background is not necessary.
		}
		catch(Exception ex) {
			world.Api.Logger.Error("[mapper] Failed to load cartography table data:\n" + ex.ToString());
			this.Chunks.Clear();
		}
	}

	public override void ToTreeAttributes(ITreeAttribute tree) {
		base.ToTreeAttributes(tree);

		System.Diagnostics.Debug.Assert(!tree.HasLargeAttribute("chunks"));
		tree.SetInt("lastUpdateID", this.lastUpdateID);
		try {
			if(this.Chunks.Count != 0) {
				byte[] data = this.Chunks.ToBytes()!;
				int numParts = tree.SetBytesLarge("chunks", data);
				this.Api.Logger.VerboseDebug($"[mapper] Cartography table at {this.Pos} saved {this.Chunks.Count} chunks taking {data.Length / 1024.0 / 1024.0:0.###} MB of space split to {numParts} attributes");
			}
		}
		catch(Exception ex) {
			this.Api.Logger.Error("[mapper] Failed to save cartography table data:\n" + ex.ToString());
		}
	}

	private void Dispose() {
		if(this.guiDialog != null) {
			this.guiDialog.TryClose();
			System.Diagnostics.Debug.Assert(this.guiDialog == null);
		}
	}

	public override void OnBlockUnloaded() {
		base.OnBlockUnloaded();
		this.Dispose();
	}

	public override void OnBlockRemoved() {
		base.OnBlockRemoved();
		this.Dispose();
	}

	public void OpenGui() {
		ICoreClientAPI capi = (ICoreClientAPI)this.Api;
		if(this.guiDialog == null) {
			this.guiDialog = new GuiDialogBlockEntityCartographyTable(this.Pos, capi);
			this.guiDialog.OnClosed += () => {
				this.guiDialog.Dispose();
				this.guiDialog = null;
			};
			this.guiDialog.TryOpen();
		}
		else
			this.guiDialog.TryClose();
	}
}
