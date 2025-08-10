namespace Mapper.Util;

using System.Collections.Generic;

public static class CoreExtensions {
	public static void Resize<T>(this List<T?> list, int size) {
		if(list.Count >= size)
			return;

		list.EnsureCapacity(size);
		for(int i = list.Count; i < size; ++i)
			list.Add(default);
	}
}
