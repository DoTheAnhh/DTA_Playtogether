using System.Windows.Media;
using DTA.Engine.Bots;
using DTA.Ui;

namespace DTA.App.Views;

/// <summary>Màu dòng trạng thái theo mức bot báo.</summary>
public static class StatusColors
{
    public static Color Of(Level level) => level switch
    {
        Level.Info => Theme.Accent,
        Level.Ok => Theme.Ok,
        Level.Warn => Theme.Warn,
        _ => Theme.Muted,
    };
}
