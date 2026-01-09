namespace Mapper.Util;

using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;

public class ItemInteractionData {
	private float duration;
	private string? animationName;
	private AssetLocation? startSound;
	private AssetLocation? loopSound;
	private float loopSoundDuration;
	private float loopSoundOffset;
	private AssetLocation? successSound;

	public void OnLoaded(CollectibleObject owner, JsonObject input) {
		this.duration = input["duration"].AsFloat(1);
		this.animationName = input["animationName"].AsString();
		this.startSound = ItemInteractionData.GetOptionalAssetLocation(owner, input["startSound"].AsString());
		this.loopSound = ItemInteractionData.GetOptionalAssetLocation(owner, input["loopSound"].AsString());
		this.loopSoundDuration = input["loopSoundDuration"].AsFloat(float.MaxValue);
		this.loopSoundOffset = input["loopSoundOffset"].AsFloat(0);
		this.successSound = ItemInteractionData.GetOptionalAssetLocation(owner, input["successSound"].AsString());
	}

	public void OnHeldInteractStart(ItemSlot slot, EntityAgent entity) {
		if(this.animationName != null)
			entity.StartAnimation(this.animationName);
		if(this.startSound != null)
			entity.World.PlaySoundAt(this.startSound, entity, (entity as EntityPlayer)?.Player);
		if(this.loopSound != null)
			slot.Itemstack.TempAttributes.SetFloat("secondsUsed", this.loopSoundOffset);
	}

	public bool OnHeldInteractStep(ItemSlot slot, EntityAgent entity, float secondsUsed) {
		if(this.loopSound != null) {
			float dt = secondsUsed - slot.Itemstack.TempAttributes.GetFloat("secondsUsed");
			if(dt >= this.loopSoundDuration) {
				entity.World.PlaySoundAt(this.loopSound, entity, (entity as EntityPlayer)?.Player);
				slot.Itemstack.TempAttributes.SetFloat("secondsUsed", secondsUsed);
			}
		}
		return secondsUsed < this.duration;
	}

	public bool OnHeldInteractStop(EntityAgent entity, float secondsUsed) {
		if(this.animationName != null)
			entity.StopAnimation(this.animationName);
		if(secondsUsed < this.duration)
			return false;

		if(this.successSound != null)
			entity.World.PlaySoundAt(this.successSound, entity, (entity as EntityPlayer)?.Player);
		return true;
	}

	private static AssetLocation? GetOptionalAssetLocation(CollectibleObject owner, string? domainAndPath) {
		if(domainAndPath == null)
			return null;

		AssetLocation assetLocation = new(domainAndPath);
		if(!assetLocation.HasDomain())
			assetLocation.Domain = owner.Code.Domain;
		return assetLocation;
	}
}
