namespace Mapper.GameContent;

using Mapper.Util;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;

public class BehaviorCartographyTableDisplay(CollectibleObject collObj) : CollectibleBehavior(collObj) {
	private CompositeShape[]? compositeShapes;
	private MeshData[]? meshes;

	public bool Valid => this.meshes != null;

	public override void Initialize(JsonObject properties) {
		base.Initialize(properties);
		this.compositeShapes = properties["shapes"].AsArray<CompositeShape>(null, this.collObj.Code.Domain);
	}

	public override void OnLoaded(ICoreAPI api) {
		base.OnLoaded(api);
		if(this.compositeShapes == null || api is not ICoreClientAPI capi) {
			this.compositeShapes = null;
			return;
		}

		CustomTextureSource textureSource = new(capi.BlockTextureAtlas);
		textureSource.LoadTextures(this.collObj);
		this.meshes = new MeshData[this.compositeShapes!.Length];
		for(int i = 0; i < this.compositeShapes.Length; ++i)
			this.meshes[i] = TesselationUtil.TesselateCompositeShape(capi, this.compositeShapes[i], textureSource);
		this.compositeShapes = null;
	}

	public MeshData GetMesh(int stackSize, int maxStackSize) {
		System.Diagnostics.Debug.Assert(stackSize > 0);
		return this.meshes![(stackSize - 1) * this.meshes.Length / maxStackSize];
	}
}
