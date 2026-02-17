namespace Mapper.Util;

using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

public static class TesselationUtil {
	public static int LoadedMeshCount { get; private set; }

	public static MeshData TesselateCompositeShape(ICoreClientAPI api, CompositeShape compositeShape, CustomTextureSource parentTextures) {
		CustomTextureSource textureSource = new(parentTextures);
		Shape shape = api.Assets.Get<Shape>(compositeShape.Base.WithPathPrefixOnce("shapes/").WithPathAppendixOnce(".json"));
		textureSource.LoadTextures(shape.Textures);

		++TesselationUtil.LoadedMeshCount;
		api.Tesselator.TesselateShape("custom shape", shape, out MeshData mesh, textureSource, null, 0, 0, 0, compositeShape.QuantityElements, compositeShape.SelectiveElements);
		if(compositeShape.Scale != 1)
			mesh.Scale(new Vec3f(), compositeShape.Scale, compositeShape.Scale, compositeShape.Scale);
		if(compositeShape.rotateX != 0 || compositeShape.rotateY != 0 || compositeShape.rotateZ != 0)
			mesh.Rotate(new Vec3f(), compositeShape.rotateX * GameMath.DEG2RAD, compositeShape.rotateY * GameMath.DEG2RAD, compositeShape.rotateZ * GameMath.DEG2RAD);
		if(compositeShape.offsetX != 0 || compositeShape.offsetY != 0 || compositeShape.offsetZ != 0)
			mesh.Translate(compositeShape.offsetX, compositeShape.offsetY, compositeShape.offsetZ);
		return mesh;
	}
}
