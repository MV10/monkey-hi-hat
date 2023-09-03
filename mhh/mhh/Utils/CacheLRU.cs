
using System.Collections;
using System.Runtime.InteropServices;

namespace mhh.Utils;

/// <summary>
/// A thread-safe size-limited Least Recently Used cache collection.
/// </summary>
public class CacheLRU<TKey, TValue> : IEnumerable<KeyValuePair<TKey, TValue>>
{
    private readonly record struct Entry(TKey Key, Lazy<TValue> Lazy);

    private readonly Dictionary<TKey, LinkedListNode<Entry>> storage;
    private readonly LinkedList<Entry> order;
    private readonly int capacity;
    private bool valueIsDisposable = false;

    private object lockStorage = new();

    public int Count { get { lock (lockStorage) return storage.Count; } }

    public CacheLRU(int maxSize, IEqualityComparer<TKey> comparer = default)
    {
        if (maxSize < 0) throw new ArgumentOutOfRangeException(nameof(maxSize));
        storage = new(maxSize + 1, comparer);
        order = new();
        capacity = maxSize;
        valueIsDisposable = typeof(TValue) is IDisposable;
    }

    /// <summary>
    /// Returns the requested item.
    /// </summary>
    public TValue Get(TKey key)
    {
        ArgumentNullException.ThrowIfNull(key);
        lock (lockStorage)
        {
            if (storage.ContainsKey(key))
            {
                ref LinkedListNode<Entry> refNode = ref CollectionsMarshal.GetValueRefOrAddDefault(storage, key, out bool exists);
                if (!ReferenceEquals(refNode, order.Last))
                {
                    order.Remove(refNode);
                    order.AddLast(refNode);
                }
                return refNode.Value.Lazy.Value;
            }
        }
        return default;
    }

    /// <summary>
    /// Attempts to add the item to the collection. Return value indicates success or failure.
    /// </summary>
    public bool TryAdd(TKey key, TValue value)
    {
        ArgumentNullException.ThrowIfNull(key);
        lock (lockStorage)
        {
            ref LinkedListNode<Entry> refNode = ref CollectionsMarshal.GetValueRefOrAddDefault(storage, key, out bool exists);
            if (exists) return false;
            var lazy = new Lazy<TValue>(value);
            refNode = new(new Entry(key, lazy));
            order.AddLast(refNode);
            if (storage.Count > capacity)
            {
                var entry = order.First.Value;
                if (valueIsDisposable)
                {
                    (entry.Lazy.Value as IDisposable).Dispose();
                }
                storage.Remove(entry.Key);
                order.RemoveFirst();
            }
        }
        return true;
    }

    /// <summary>
    /// Indicates whether the key exists in the collection.
    /// </summary>
    public bool ContainsKey(TKey key)
    {
        ArgumentNullException.ThrowIfNull(key);
        lock (lockStorage)
        {
            return storage.ContainsKey(key);
        }
    }

    /// <summary>
    /// Removes an item from the collection.
    /// </summary>
    public void Remove(TKey key)
    {
        ArgumentNullException.ThrowIfNull(key);
        lock (lockStorage)
        {
            if (storage.ContainsKey(key))
            {
                storage.Remove(key);
                ref LinkedListNode<Entry> refNode = ref CollectionsMarshal.GetValueRefOrAddDefault(storage, key, out bool exists);
                if(valueIsDisposable)
                {
                    (refNode.Value.Lazy.Value as IDisposable).Dispose();
                }
                order.Remove(refNode);
            }
        }
    }

    /// <summary>
    /// Removes all items from the collection.
    /// </summary>
    public void Clear()
    {
        lock (lockStorage)
        {
            if(valueIsDisposable)
            {
                foreach (var obj in order)
                {
                    (obj.Lazy.Value as IDisposable).Dispose();
                }
            }
            storage.Clear();
            order.Clear();
        }
    }

    /// <summary>
    /// Returns the IEnumerator for the collection.
    /// </summary>
    public IEnumerator<KeyValuePair<TKey, TValue>> GetEnumerator()
    {
        lock (lockStorage)
        {
            return order
                .ToArray()
                .Select((Entry e) => KeyValuePair.Create(e.Key, e.Lazy.Value))
                .AsEnumerable()
                .GetEnumerator();
        }
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
