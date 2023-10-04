
using Microsoft.Extensions.Logging;
using System.Collections;
using System.Runtime.InteropServices;

namespace mhh;

/// <summary>
/// A thread-safe size-limited Least Recently Used cache collection.
/// </summary>
public class CacheLRU<TKey, TValue> : IEnumerable<KeyValuePair<TKey, TValue>>, IDisposable
{
    private readonly record struct Entry(TKey Key, Lazy<TValue> Lazy);

    private readonly Dictionary<TKey, LinkedListNode<Entry>> Storage;
    private readonly LinkedList<Entry> Sequence;
    private readonly int Capacity;
    private bool CachedValuesAreDisposable = false;

    private object LockStorage = new();

    public int Count { get { lock (LockStorage) return Storage.Count; } }

    // When caching is disabled, the cache is only purged as cache keys are
    // accessed. Caching can be re-enabled at any time and the existing cached
    // items should still be valid for re-use.
    public bool CachingDisabled { get; set; }

    public CacheLRU(int maxSize, IEqualityComparer<TKey> comparer = default)
    {
        if (maxSize < 0) throw new ArgumentOutOfRangeException(nameof(maxSize));
        Storage = new(maxSize + 1, comparer);
        Sequence = new();
        Capacity = maxSize;
        CachedValuesAreDisposable = typeof(TValue) is IDisposable;
    }

    /// <summary>
    /// Returns the requested item.
    /// </summary>
    public TValue Get(TKey key)
    {
        ArgumentNullException.ThrowIfNull(key);
        lock (LockStorage)
        {
            if (Storage.ContainsKey(key))
            {
                if(CachingDisabled)
                {
                    Remove(key);
                    return default;
                }

                ref LinkedListNode<Entry> refNode = ref CollectionsMarshal.GetValueRefOrAddDefault(Storage, key, out bool exists);
                if (!ReferenceEquals(refNode, Sequence.Last))
                {
                    Sequence.Remove(refNode);
                    Sequence.AddLast(refNode);
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
        lock (LockStorage)
        {
            if(CachingDisabled)
            {
                if (Storage.ContainsKey(key)) Remove(key);
                return false;
            }

            ref LinkedListNode<Entry> refNode = ref CollectionsMarshal.GetValueRefOrAddDefault(Storage, key, out bool exists);
            if (exists) return false;
            var lazy = new Lazy<TValue>(value);
            refNode = new(new Entry(key, lazy));
            Sequence.AddLast(refNode);
            if (Storage.Count > Capacity)
            {
                var entry = Sequence.First.Value;
                if (CachedValuesAreDisposable)
                {
                    (entry.Lazy.Value as IDisposable).Dispose();
                }
                Storage.Remove(entry.Key);
                Sequence.RemoveFirst();
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
        lock (LockStorage)
        {
            var found = Storage.ContainsKey(key);

            if(CachingDisabled && found)
            {
                Remove(key);
                return false;
            }

            return found;
        }
    }

    /// <summary>
    /// Removes an item from the collection.
    /// </summary>
    public void Remove(TKey key)
    {
        ArgumentNullException.ThrowIfNull(key);
        lock (LockStorage)
        {
            if (Storage.ContainsKey(key))
            {
                ref LinkedListNode<Entry> refNode = ref CollectionsMarshal.GetValueRefOrAddDefault(Storage, key, out bool exists);
                if(CachedValuesAreDisposable)
                {
                    (refNode.Value.Lazy.Value as IDisposable).Dispose();
                }
                Sequence.Remove(refNode);
                Storage.Remove(key);
            }
        }
    }

    /// <summary>
    /// Returns the IEnumerator for the collection.
    /// </summary>
    public IEnumerator<KeyValuePair<TKey, TValue>> GetEnumerator()
    {
        lock (LockStorage)
        {
            if (CachingDisabled) return new KeyValuePair<TKey, TValue>[0].AsEnumerable().GetEnumerator();

            return Sequence
                .ToArray()
                .Select((Entry e) => KeyValuePair.Create(e.Key, e.Lazy.Value))
                .AsEnumerable()
                .GetEnumerator();
        }
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    public void Dispose()
    {
        if (IsDisposed) return;
        LogHelper.Logger?.LogTrace($"{GetType()}.Dispose() ----------------------------");

        lock (LockStorage)
        {
            if (CachedValuesAreDisposable)
            {
                foreach (var obj in Sequence)
                {
                    LogHelper.Logger?.LogTrace($"  {GetType()}.Dispose() key {obj.Key}");
                    (obj.Lazy.Value as IDisposable).Dispose();
                }
            }
            Storage.Clear();
            Sequence.Clear();
        }

        IsDisposed = true;
        GC.SuppressFinalize(true);
    }
    private bool IsDisposed = false;
}
