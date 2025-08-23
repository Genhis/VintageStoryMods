namespace Mapper.Behaviors;

using Newtonsoft.Json.Linq;
using System;
using System.Text;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;

public class BehaviorCompassNeedle : CollectibleBehavior {
	private string? error;

	public int Resolution { get; private set; }
	public float Omega { get; private set; }
	public float SpringDampingRatio { get; private set; }

	public BehaviorCompassNeedle(CollectibleObject collObj) : base(collObj) {}

	public override void Initialize(JsonObject properties) {
		base.Initialize(properties);
		if(this.collObj is not Item item) {
			this.error = "MapperCompassNeedle behavior can't be used on blocks";
			return;
		}

		this.Resolution = Math.Max(properties["resolution"].AsInt(8), 0);
		this.Omega = properties["springFrequency"].AsFloat(100) * 2 * GameMath.PI;
		this.SpringDampingRatio = properties["springDampingRatio"].AsFloat(1);
		if(item.Shape == null)
			return; // The server hasn't sent any shape information yet. It doesn't matter, the changes should already be applied when the shape is received.

		CompositeShape needleShape = properties["needleShape"].AsObject<CompositeShape>();
		if(needleShape == null) {
			this.error = "Property 'needleShape' of MapperCompassNeedle behavior does not exist";
			this.Resolution = 0;
			properties["resolution"].Token = JToken.FromObject(0);
			return;
		}
		if(!needleShape.Base.HasDomain())
			needleShape.Base.Domain = this.collObj.Code.Domain;
		needleShape.Overlays = [item.Shape];
		CompositeShape rotatedShape = needleShape.CloneWithoutAlternates();

		// The main shape is reserved for in-world display, all rotation shapes are in alternates.
		float offset = properties["rotationOffset"].AsFloat(0);
		float step = 360.0f / this.Resolution;
		needleShape.Alternates = new CompositeShape[this.Resolution];
		for(int i = 0; i < this.Resolution; i++) {
			rotatedShape.rotateY = i * step + offset;
			needleShape.Alternates[i] = rotatedShape.Clone();
		}
		item.Shape = needleShape;
	}

	public override void OnLoaded(ICoreAPI api) {
		base.OnLoaded(api);
		if(this.error != null)
			api.Logger.Error($"{this.error} ({this.collObj.Code})");
	}

	public override void GetHeldItemInfo(ItemSlot slot, StringBuilder description, IWorldAccessor world, bool withDebugInfo) {
		base.GetHeldItemInfo(slot, description, world, withDebugInfo);

		int? scaleFactor = BehaviorCompassNeedle.GetScaleFactor(this.collObj.Attributes);
		if(scaleFactor != null)
			description.AppendLine(Lang.Get("mapper:iteminfo-compass-resolution", scaleFactor.Value));
	}

	internal BehaviorCompassNeedle? GetIfValid() {
		return this.Resolution == 0 ? null : this;
	}

	public static int? GetScaleFactor(JsonObject? itemAttributes) {
		JsonObject? compassZoomLevel = itemAttributes?["mapper"]["compassZoomLevel"];
		return compassZoomLevel != null && compassZoomLevel.Exists ? 1 << Math.Clamp(compassZoomLevel.AsInt(0), 0, 30) : null;
	}
}

internal class CompassNeedleUpdater {
	private struct HandData {
		public ItemStack? Stack;
		public BehaviorCompassNeedle? Behavior;
		public float CompassYaw;
		public float CompassVelocity;
	}

	private readonly ICoreClientAPI api;
	private readonly long listenerId;
	private HandData leftHand;
	private HandData rightHand;

	public CompassNeedleUpdater(ICoreClientAPI api) {
		this.api = api;
		this.listenerId = api.Event.RegisterGameTickListener(this.OnTick, 0);
	}

	public void Dispose() {
		this.api.Event.UnregisterGameTickListener(this.listenerId);
	}

	private void OnTick(float dt) {
		if(this.api.IsGamePaused)
			return;

		dt = Math.Min(dt, 2 / 30f);
		EntityPlayer playerEntity = this.api.World.Player.Entity;
		float yaw = playerEntity.Pos.Yaw;
		HandData stashedLeftHand = this.leftHand;
		CompassNeedleUpdater.CheckHand(playerEntity.LeftHandItemSlot.Itemstack, HandType.Left, ref this.leftHand, this.rightHand, yaw);
		CompassNeedleUpdater.CheckHand(playerEntity.RightHandItemSlot.Itemstack, HandType.Right, ref this.rightHand, stashedLeftHand, yaw);
		CompassNeedleUpdater.UpdateHand(ref this.leftHand, yaw, dt);
		CompassNeedleUpdater.UpdateHand(ref this.rightHand, yaw, dt);
	}

	private enum HandType { None, Left, Right };
	private static void CheckHand(ItemStack? currentStack, HandType handType, ref HandData hand, HandData otherHand, float yaw) {
		if(currentStack == hand.Stack)
			return;
		if(currentStack == otherHand.Stack) {
			hand = otherHand;
			hand.Stack?.TempAttributes.SetInt("mapper:inHand", (int)handType);
			return;
		}

		// ItemStack reference could be changed one tick later when hands are swapped. Update it and don't change anything else in this case.
		bool isSameHand = currentStack != null && handType == (HandType)currentStack.TempAttributes.GetInt("mapper:inHand", (int)HandType.None);
		if(!isSameHand && hand.Behavior != null) {
			hand.Stack!.TempAttributes.SetInt("renderVariant", 0);
			hand.Stack.TempAttributes.SetInt("mapper:inHand", (int)HandType.None);
		}

		hand.Stack = currentStack;
		hand.Behavior = hand.Stack?.Collectible.GetBehavior<BehaviorCompassNeedle>()?.GetIfValid();

		if(!isSameHand && hand.Behavior != null) {
			hand.Stack!.TempAttributes.SetInt("mapper:inHand", (int)handType);
			hand.CompassYaw = yaw;
			hand.CompassVelocity = 1;
		}
	}

	private static void UpdateHand(ref HandData hand, float targetYaw, float dt) {
		if(hand.Behavior == null)
			return;

		float diff = NormalizeAngleRad(targetYaw - hand.CompassYaw);
		float acceleration = hand.Behavior.Omega * hand.Behavior.Omega * diff - 2 * hand.Behavior.SpringDampingRatio * hand.Behavior.Omega * hand.CompassVelocity;
		hand.CompassVelocity += acceleration * dt;
		hand.CompassYaw = NormalizeAngleRad(hand.CompassYaw + hand.CompassVelocity * dt);
		hand.Stack!.TempAttributes.SetInt("renderVariant", (int)MathF.Round((hand.CompassYaw / GameMath.TWOPI + 1) * hand.Behavior.Resolution) % hand.Behavior.Resolution + 2);
	}

	private static float NormalizeAngleRad(float angle) {
		while(angle > GameMath.PI)
			angle -= GameMath.TWOPI;
		while(angle < -GameMath.PI)
			angle += GameMath.TWOPI;
		return angle;
	}
}
