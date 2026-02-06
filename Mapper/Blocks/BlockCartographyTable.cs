namespace Mapper.Blocks;

using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

public class BlockCartographyTable : Block {
	public override bool TryPlaceBlock(IWorldAccessor world, IPlayer player, ItemStack stack, BlockSelection selection, ref string failureCode) {
		if(!this.CanPlaceBlock(world, player, selection, ref failureCode))
			return false;

		BlockFacing facing = Block.SuggestedHVOrientation(player, selection)[0];
		BlockSelection secondSelection = new() {Position = selection.Position.AddCopy(facing.GetCW()), Face = BlockFacing.UP};
		if(!this.CanPlaceBlock(world, player, secondSelection, ref failureCode))
			return false;

		world.BlockAccessor.GetBlock(this.CodeWithParts("left", facing.Code)).DoPlaceBlock(world, player, selection, stack);
		world.BlockAccessor.GetBlock(this.CodeWithParts("right", facing.Code)).DoPlaceBlock(world, player, secondSelection, stack);
		return true;
	}

	public override void OnBlockRemoved(IWorldAccessor world, BlockPos position) {
		string side = this.LastCodePart(1);
		BlockFacing facing = BlockFacing.FromCode(this.LastCodePart());

		BlockPos secondPosition = position.AddCopy(side == "left" ? facing.GetCW() : facing.GetCCW());
		Block secondBlock = world.BlockAccessor.GetBlock(secondPosition);
		if(secondBlock is BlockCartographyTable && secondBlock.LastCodePart(1) != side)
			world.BlockAccessor.SetBlock(0, secondPosition);

		base.OnBlockRemoved(world, position);
	}

	public override ItemStack OnPickBlock(IWorldAccessor world, BlockPos position) {
		return new ItemStack(world.BlockAccessor.GetBlock(this.CodeWithParts("left", "north")));
	}

	public override AssetLocation GetHorizontallyFlippedBlockCode(EnumAxis axis) {
		BlockFacing facing = BlockFacing.FromCode(this.LastCodePart());
		return this.CodeWithParts(this.LastCodePart(1) == "left" ? "right" : "left", facing.Axis == axis ? facing.Opposite.Code : facing.Code);
	}

	public override AssetLocation GetRotatedBlockCode(int angle) {
		int rotatedIndex = GameMath.Mod(BlockFacing.FromCode(this.LastCodePart()).HorizontalAngleIndex - angle / 90, 4);
		return this.CodeWithParts(BlockFacing.HORIZONTALS_ANGLEORDER[rotatedIndex].Code);
	}
}
