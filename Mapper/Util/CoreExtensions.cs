namespace Mapper.Util;

using Mapper.Util;
using System;
using System.Collections.Generic;
using System.Threading;

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

	public static IDisposable ExclusiveLock(this ReaderWriterLockSlim rwLock) {
		rwLock.EnterWriteLock();
		return new DisposableAction(rwLock.ExitWriteLock);
	}

	/// <summary>This lock is not contended unless another thread requests an exclusive lock.</summary>
	public static IDisposable SharedLock(this ReaderWriterLockSlim rwLock) {
		rwLock.EnterReadLock();
		return new DisposableAction(rwLock.ExitReadLock);
	}
}
