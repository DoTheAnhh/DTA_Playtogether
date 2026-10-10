namespace DTA.Game.Actions;

/// <summary>Bộ xử lý 1 loại bảng game; thêm loại bảng = thêm 1 class, không sửa router.</summary>
public interface IDialogHandler
{
    /// <summary>Bảng có class (hoặc class cha) tên <paramref name="names"/> thuộc bộ này không.</summary>
    bool Matches(IReadOnlyList<string> names);

    /// <summary>Làm <paramref name="action"/> với bảng; true = đã xử lý.</summary>
    bool Handle(DialogContext ctx, long dialog, DialogAction action);
}

/// <summary>Bộ xử lý nhận diện theo 1 tên class.</summary>
public abstract class ClassHandler(string className) : IDialogHandler
{
    public string ClassName { get; } = className;

    public bool Matches(IReadOnlyList<string> names) => names.Any(n => n.Contains(ClassName, StringComparison.Ordinal));

    public abstract bool Handle(DialogContext ctx, long dialog, DialogAction action);
}
