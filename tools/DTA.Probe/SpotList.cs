using DTA.Features.Excavation;
using DTA.Game.Session;
using DTA.Runtime.Core;

/// <summary>Chỉ đọc: liệt kê điểm đào + State (ExcavationObjectManager.ObjectState) + máu + khoảng cách: spots.</summary>
public static class SpotList
{
    public static void Run(GameSession session, Action<string> step)
    {
        var game = new ExcavationGame(session);
        var spots = game.Spots();
        var me = session.Player.Position();
        foreach (var s in spots.Values.OrderBy(s => me is { } p ? (s.X - p.X) * (s.X - p.X) + (s.Z - p.Z) * (s.Z - p.Z) : 0))
        {
            var raw = session.Memory.Read(s.Ref + session.Il2Cpp.Field(session.Managed.ClassOf(s.Ref), "State"), 4);
            var d = me is { } q ? MathF.Sqrt((s.X - q.X) * (s.X - q.X) + (s.Z - q.Z) * (s.Z - q.Z)) : 0;
            step($"uid {s.Uid} state {(raw != null ? Bin.I32(raw, 0) : -1)} kind {s.Kind} hp {s.Hp}/{s.MaxHp} ({s.X:F1},{s.Z:F1}) {d:F1} m");
        }
    }
}
