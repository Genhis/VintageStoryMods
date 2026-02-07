namespace Mapper.Util;

using System.Collections.Generic;

public static class CoreExtensions {
	public static V GetOrCreate<K, V>(this Dictionary<K, V> dictionary, K key) where K: notnull where V: new() {
		return dictionary.TryGetValue(key, out V? value) ? value : (dictionary[key] = new V());
	}

	public static void Resize<T>(this List<T?> list, int size) {
		if(list.Count >= size)
			return;

		list.EnsureCapacity(size);
		for(int i = list.Count; i < size; ++i)
			list.Add(default);
	}
}
