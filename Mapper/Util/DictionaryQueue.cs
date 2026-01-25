namespace Mapper.Util;

using System.Collections;
using System.Collections.Generic;

#nullable disable

public class DictionaryQueue<TKey, TValue> : IEnumerable<KeyValuePair<TKey, TValue>> where TKey: notnull {
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

	public bool Remove(TKey key) {
		return this.dictionary.Remove(key);
	}

	public void Clear() {
		this.dictionary.Clear();
		this.queue.Clear();
	}

	public void EnsureCapacity(int capacity) {
		this.dictionary.EnsureCapacity(capacity);
		this.queue.EnsureCapacity(capacity);
	}

	public IEnumerator<KeyValuePair<TKey, TValue>> GetEnumerator() {
		foreach(TKey key in this.queue)
			yield return new KeyValuePair<TKey, TValue>(key, this.dictionary[key]);
	}

	IEnumerator IEnumerable.GetEnumerator() => this.GetEnumerator();
}
