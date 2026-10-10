using System.Collections.Concurrent;

namespace DTA.Runtime.Il2Cpp;

/// <summary>Bố cục 1 class lưu theo phiên bản game: tên đầy đủ, khoá class cha, field khai báo ngay trong class.</summary>
public sealed class ClassLayout
{
    public string Name { get; set; } = "";
    public string Parent { get; set; } = "";
    public Dictionary<string, int> Fields { get; set; } = new();
}

/// <summary>
/// Dữ liệu il2cpp dò được, không đổi trong 1 phiên bản game - lưu trong VersionCache nên lần kết nối sau gần như không dò gì:
/// ô chứa con trỏ class (offset trong vùng dữ liệu libil2cpp), bố cục class, tên class theo vị trí trong khối metadata.
/// Dùng chung nhiều luồng nên là ConcurrentDictionary.
/// </summary>
public sealed class Il2CppLayouts
{
    public ConcurrentDictionary<string, long> Slots { get; set; } = new();
    public ConcurrentDictionary<string, ClassLayout> Layouts { get; set; } = new();
    public ConcurrentDictionary<string, string> Texts { get; set; } = new();
}
