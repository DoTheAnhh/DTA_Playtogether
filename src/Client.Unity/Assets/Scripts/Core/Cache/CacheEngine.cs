namespace DTA.Core.Cache;

public interface ICache<TKey, TValue> where TKey : notnull
{
    bool TryGet(TKey key, out TValue? value);
    void Set(TKey key, TValue value, TimeSpan? ttl = null, IReadOnlyList<string>? tags = null);
    bool Remove(TKey key);
    int InvalidateByTag(string tag);
    void Clear();
    long Hits { get; }
    long Misses { get; }
    double HitRate { get; }
}

public sealed class MemoryCache<TKey, TValue> : ICache<TKey, TValue> where TKey : notnull
{
    private sealed class Entry(TValue value, DateTime expireAt, IReadOnlyList<string>? tags)
    {
        public TValue Value { get; set; } = value;
        public DateTime ExpireAt { get; set; } = expireAt;
        public IReadOnlyList<string> Tags { get; set; } = tags ?? [];
        public long LastAccess { get; set; } = Environment.TickCount64;
    }

    private readonly Dictionary<TKey, Entry> _entries = [];
    private readonly Dictionary<string, HashSet<TKey>> _tagIndex = [];
    private readonly object _lock = new();
    private readonly int _maxEntries;
    private long _hits;
    private long _misses;

    public long Hits => Interlocked.Read(ref _hits);
    public long Misses => Interlocked.Read(ref _misses);
    public double HitRate
    {
        get
        {
            long total = Hits + Misses;
            return total == 0 ? 0.0 : (double)Hits / total;
        }
    }

    public MemoryCache(int maxEntries = 1000)
    {
        _maxEntries = maxEntries;
    }

    public bool TryGet(TKey key, out TValue? value)
    {
        lock (_lock)
        {
            if (_entries.TryGetValue(key, out var entry))
            {
                if (DateTime.UtcNow <= entry.ExpireAt)
                {
                    entry.LastAccess = Environment.TickCount64;
                    Interlocked.Increment(ref _hits);
                    value = entry.Value;
                    return true;
                }
                // Expired
                RemoveInternal(key);
            }
            Interlocked.Increment(ref _misses);
            value = default;
            return false;
        }
    }

    public void Set(TKey key, TValue value, TimeSpan? ttl = null, IReadOnlyList<string>? tags = null)
    {
        var expireAt = DateTime.UtcNow.Add(ttl ?? TimeSpan.FromMinutes(30));
        lock (_lock)
        {
            if (_entries.Count >= _maxEntries && !_entries.ContainsKey(key))
            {
                // Evict LRU entry
                var oldest = _entries.MinBy(kv => kv.Value.LastAccess);
                if (oldest.Key != null)
                {
                    RemoveInternal(oldest.Key);
                }
            }

            if (_entries.TryGetValue(key, out var existing))
            {
                // Remove from existing tags
                foreach (var tag in existing.Tags)
                {
                    if (_tagIndex.TryGetValue(tag, out var set))
                        set.Remove(key);
                }
            }

            var entry = new Entry(value, expireAt, tags);
            _entries[key] = entry;

            if (tags != null)
            {
                foreach (var tag in tags)
                {
                    if (!_tagIndex.TryGetValue(tag, out var set))
                    {
                        set = [];
                        _tagIndex[tag] = set;
                    }
                    set.Add(key);
                }
            }
        }
    }

    public bool Remove(TKey key)
    {
        lock (_lock)
        {
            return RemoveInternal(key);
        }
    }

    public int InvalidateByTag(string tag)
    {
        lock (_lock)
        {
            if (!_tagIndex.TryGetValue(tag, out var keys))
                return 0;

            int count = 0;
            var list = keys.ToList();
            foreach (var key in list)
            {
                if (RemoveInternal(key))
                    count++;
            }
            _tagIndex.Remove(tag);
            return count;
        }
    }

    public void Clear()
    {
        lock (_lock)
        {
            _entries.Clear();
            _tagIndex.Clear();
        }
    }

    private bool RemoveInternal(TKey key)
    {
        if (_entries.Remove(key, out var entry))
        {
            foreach (var tag in entry.Tags)
            {
                if (_tagIndex.TryGetValue(tag, out var set))
                {
                    set.Remove(key);
                    if (set.Count == 0)
                        _tagIndex.Remove(tag);
                }
            }
            return true;
        }
        return false;
    }
}
