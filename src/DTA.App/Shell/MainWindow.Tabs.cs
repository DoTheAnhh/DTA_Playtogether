using System.Windows;
using System.Windows.Threading;
using DTA.Ui;
using DTA.App.Views;

namespace DTA.App.Shell;

/// <summary>
/// Tab: mỗi tab là 1 nhóm phần tử trên mặt vẽ (kể cả chân trang + hàng tab của trang chứa nó); đổi tab = ẩn nhóm cũ, hiện nhóm mới (chỉ đụng
/// phần tử đổi trạng thái). Chỉ dựng trang sắp hiện; các trang khác dựng ngầm lúc rảnh để bấm menu là mở tức thì.
/// </summary>
public sealed partial class MainWindow
{
    private readonly Dictionary<string, List<UIElement>> _tabItems = [];
    private readonly Dictionary<string, PageView> _tabView = [];
    private readonly Dictionary<PageView, string> _viewTab = [];
    private readonly Dictionary<PageView, CSegment<string>> _segments = [];
    private readonly List<PageView> _unbuilt = [];
    private HashSet<UIElement> _shown = [];

    private void SetupTabs()
    {
        foreach (var view in Views)
        {
            foreach (var (key, _) in view.Tabs) _tabView[key] = view;
            _viewTab[view] = view.Tabs[0].Key;
        }
        _unbuilt.AddRange(Views.Where(v => v != CurrentView));
        BuildPage(CurrentView);
        ShowTab(_viewTab.TryGetValue(CurrentView, out var tab) ? tab : CurrentView.Tabs[0].Key);
    }

    private bool TabsFit(PageView view) => Layout.TabW * view.Tabs.Count <= Layout.TabRoom;

    /// <summary>Mép trên vùng nội dung của 1 trang (thấp hơn khi hàng tab phải nằm riêng 1 dòng).</summary>
    public double ContentTop(PageView view) => TabsFit(view) ? Layout.ContentTop : Layout.ContentTop + CSegment<string>.H + 12;

    /// <summary>Dựng trang: chân trang, hàng tab (từ 2 tab), nội dung từng tab. Trang không đang xem thì ẩn ngay (trước lần vẽ kế tiếp nên không nháy).</summary>
    private void BuildPage(PageView view)
    {
        var shared = Board.Collect(view.BuildFooter);
        if (view.Tabs.Count > 1)
        {
            var (y, width) = TabsFit(view) ? (Layout.BarTop, Layout.TabW * view.Tabs.Count) : (Layout.ContentTop, Layout.Right - Layout.Left);
            shared.AddRange(Board.Collect(() => _segments[view] = new CSegment<string>(Board, Layout.Left, y, width, view.Tabs, ShowTab)));
        }
        foreach (var (key, _) in view.Tabs)
        {
            var items = Board.Collect(() => view.BuildTab(key));
            items.AddRange(shared);
            _tabItems[key] = items;
            if (view == CurrentView) _shown.UnionWith(items);
            else foreach (var item in items) item.Visibility = Visibility.Hidden;
        }
    }

    /// <summary>Dựng ngầm từng trang còn lại lúc giao diện rảnh.</summary>
    private void PrebuildLater()
    {
        if (_unbuilt.Count == 0) return;
        Dispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, () =>
        {
            if (_unbuilt.Count == 0) return;
            var view = _unbuilt[0];
            _unbuilt.RemoveAt(0);
            try
            {
                BuildPage(view);
                if (Device != null) view.OnDevice(Device);
            }
            catch (Exception e)
            {
                L.Error($"Dựng trang {view.Name}: {e}");
            }
            PrebuildLater();
        });
    }

    /// <summary>Mở 1 tab (kéo theo trang của chức năng chứa tab đó).</summary>
    public void ShowTab(string key)
    {
        var view = CurrentView = _tabView[key];
        if (_unbuilt.Remove(view))
        {
            BuildPage(view);
            view.OnDevice(Device);
        }
        _viewTab[view] = key;
        _nav.Set(view.Name);
        if (_segments.TryGetValue(view, out var segment)) segment.Set(key);
        var visible = _tabItems[key];
        var next = new HashSet<UIElement>(visible);
        foreach (var item in _shown) if (!next.Contains(item)) item.Visibility = Visibility.Hidden;
        foreach (var item in visible) item.Visibility = Visibility.Visible;
        _shown = next;
        _title.Text = view.Name;
        _subtitle.Text = view.Subtitle;
        _title.Block.UpdateLayout();
        _help.MoveTo(Layout.Left + _title.Block.DesiredSize.Width + 14, 16);
        if (Settings.Page == view.Name) return;
        Settings.Page = view.Name;
        Settings.Save();
    }

    /// <summary>Các trang đã dựng (đã mở ít nhất 1 lần hoặc dựng ngầm xong).</summary>
    public IEnumerable<PageView> Built() => Views.Where(v => !_unbuilt.Contains(v));
}
