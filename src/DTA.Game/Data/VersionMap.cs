using System.Collections.Concurrent;
using DTA.Runtime.Storage;

namespace DTA.Game.Data;

/// <summary>Bảng tra số -> giá trị nhớ theo phiên bản game (tên vật phẩm, giá, số lượt dụng cụ...): đọc từ bộ đệm 1 lần, ghi lại khi thêm.</summary>
public sealed class VersionMap<T>
{
    private readonly VersionCache _cache;
    private readonly string _key;
    private readonly ConcurrentDictionary<long, T> _map;

    public VersionMap(VersionCache cache, string key)
    {
        (_cache, _key) = (cache, key);
        _map = new ConcurrentDictionary<long, T>(cache.Get<Dictionary<long, T>>(key) ?? []);
    }

    public bool TryGet(long id, out T value) => _map.TryGetValue(id, out value!);

    /// <summary>Thêm / đổi 1 mục và đánh dấu bộ đệm cần lưu.</summary>
    public void Set(long id, T value)
    {
        _map[id] = value;
        _cache.Put(_key, new Dictionary<long, T>(_map));
    }

    /// <summary>Giá trị có sẵn hoặc đọc mới (null = chưa đọc được, không nhớ để lần sau thử lại).</summary>
    public T? GetOrRead(long id, Func<T?> read)
    {
        if (_map.TryGetValue(id, out var value)) return value;
        var found = read();
        if (found != null) Set(id, found);
        return found;
    }

    public int Count => _map.Count;
    public IEnumerable<KeyValuePair<long, T>> Items => _map;
}
