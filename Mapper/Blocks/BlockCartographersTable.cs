namespace Mapper.Blocks;

using Mapper.WorldMap;
using Vintagestory.API.Common;
using Vintagestory.API.Client;
using Vintagestory.API.Server;
using Vintagestory.API.Config;
using Vintagestory.API.Util;

public class BlockCartographersTable : Block {
	public override bool OnBlockInteractStart(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel) {
		if(world.Side == EnumAppSide.Client) {
			ICoreClientAPI capi = (ICoreClientAPI)this.api;
			MapperChunkMapLayer? mapLayer = MapperChunkMapLayer.GetInstance(capi);

			if(mapLayer == null || !mapLayer.Enabled) {
				capi.TriggerIngameError(this, "mapper-disabled", Lang.Get("mapper:error-cartographers-table-disabled"));
				return true;
			}

			mapLayer.SendSyncWithTableRequest(blockSel.Position);
			return true;
		}

		return true;
	}

	public override WorldInteraction[] GetPlacedBlockInteractionHelp(IWorldAccessor world, BlockSelection selection, IPlayer forPlayer) {
		return new WorldInteraction[] {
			new WorldInteraction {
				ActionLangCode = "mapper:blockhelp-cartographers-table-sync",
				MouseButton = EnumMouseButton.Right
			}
		}.Append(base.GetPlacedBlockInteractionHelp(world, selection, forPlayer));
	}
}
