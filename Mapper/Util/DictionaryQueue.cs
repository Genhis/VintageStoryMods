namespace Mapper.Util;

using System.Collections.Generic;

#nullable disable

public class DictionaryQueue<TKey, TValue> where TKey: notnull {
	private readonly Dictionary<TKey, TValue> dictionary = [];
	private readonly Queue<TKey> queue = [];

	public int Count => this.dictionary.Count;

	public void Enqueue(KeyValuePair<TKey, TValue> item) {
		if(!this.dictionary.ContainsKey(item.Key))
			this.queue.Enqueue(item.Key);
		this.dictionary[item.Key] = item.Value;
	}

	public KeyValuePair<TKey, TValue> Dequeue() {
		TKey key = this.queue.Dequeue();
		this.dictionary.Remove(key, out TValue value);
		return new(key, value);
	}
}
