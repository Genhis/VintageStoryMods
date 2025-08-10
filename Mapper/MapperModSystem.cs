namespace Mapper;

using Mapper.Items;
using Vintagestory.API.Common;

public class MapperModSystem : ModSystem {
	private static bool enabled = true;

	public override void Start(ICoreAPI api) {
		base.Start(api);

		if(MapperModSystem.enabled) {
			api.RegisterItemClass("MapperMap", typeof(ItemMap));
			api.RegisterItemClass("MapperPaintbrush", typeof(ItemPaintbrush));
		}
	}
}
