using System.Windows.Threading;
using DTA.Ui;
using DTA.Engine.Core;
using DTA.Engine.Popups;
using DTA.Runtime.Core;
using DTA.Runtime.Device;

namespace DTA.App.Shell;

/// <summary>Hàng chọn giả lập + tab giả lập, thời hạn key, huy hiệu lỗi của menu Log.</summary>
public sealed partial class MainWindow
{
    private CButton _refresh = null!;
    private CCombo _emulatorBox = null!, _instanceBox = null!;
    private List<EmulatorInstance> _instances = [];
    private bool _running;

    /// <summary>Tab giả lập đang chọn (null = chưa có).</summary>
    public EmulatorDevice? Device { get; private set; }

    /// <summary>Bên phải hàng dưới tiêu đề: giả lập, tab giả lập, nút quét lại.</summary>
    private void BuildBar()
    {
        const double y = Layout.BarTop, right = Layout.Right;
        _refresh = new CButton(Board, right - 32, y + 1, 32, 32, "↻", RefreshDevices, Theme.GhostFill, Theme.GhostHover, Theme.Text, 10, 12);
        _instanceBox = new CCombo(Board, right - 40 - 214, y + 1, 214, 32, [], OnInstanceSelected);
        Board.Text(right - 262, y + 17, "Tab", Anchor.E, Theme.Muted, 8, true);
        _emulatorBox = new CCombo(Board, right - 292 - 116, y + 1, 116, 32, Emulators.All.Select(e => e.Name), RefreshDevices);
        _emulatorBox.Select(Math.Max(0, Emulators.All.ToList().FindIndex(e => e.Name == Settings.Emulator)));
        Board.Text(right - 416, y + 17, "Giả lập", Anchor.E, Theme.Muted, 8, true);
    }

    /// <summary>Liệt kê lại các tab đang chạy của giả lập đang chọn (chạy nền vì phải gọi chương trình ngoài).</summary>
    private void RefreshDevices()
    {
        if (Program.Offline) return;
        var emulator = Emulators.All[Math.Max(0, _emulatorBox.Index)];
        _refresh.SetEnabled(false);
        Task.Run(() =>
        {
            var instances = emulator.Instances();
            var adb = emulator.AdbPath;
            Ui(() => OnDevicesListed(emulator, instances, adb));
        });
    }

    private void OnDevicesListed(IEmulator emulator, List<EmulatorInstance> instances, string adb)
    {
        _refresh.SetEnabled(!_running);
        if (_running) return;
        _instances = instances;
        _instanceBox.SetValues(instances.Select(i => i.ToString()));
        if (instances.Count == 0)
        {
            _instanceBox.SetText("");
            SelectDevice(null);
            Notify(adb.Length > 0 ? "Chưa có tab giả lập nào đang chạy" : $"Không tìm thấy {emulator.Name} trên máy", Theme.Warn);
            return;
        }
        var wanted = Device?.Serial ?? Settings.Device;
        _instanceBox.Select(Math.Max(0, instances.FindIndex(i => i.Serial == wanted)));
        OnInstanceSelected();
    }

    private void OnInstanceSelected()
    {
        var index = _instanceBox.Index;
        if (index < 0 || index >= _instances.Count) return;
        var instance = _instances[index];
        if (Device?.Serial == instance.Serial && Device.Emulator == instance.Emulator) return;
        SelectDevice(EmulatorDevice.Of(instance));
        (Settings.Emulator, Settings.Device) = (instance.Emulator.Name, instance.Serial);
        Settings.Save();
    }

    /// <summary>Gắn cửa sổ với 1 tab giả lập: canh popup chung + báo mọi trang đã dựng (mỗi bước riêng: 1 trang lỗi không chặn trang khác).</summary>
    private void SelectDevice(EmulatorDevice? device)
    {
        Device = device;
        Title = device != null ? $"{Layout.AppName} - {_instanceBox.Value}" : Layout.AppName;
        _nav.SetEnabled(device != null);
        _pillText.Text = device != null ? "●  Đã dừng" : "●  Chưa chọn tab";
        var steps = new List<(string, Action)> { ("PopupWatcher", () => PopupWatcher.Instance.SetDevice(device)) };
        steps.AddRange(Built().Select(view => (view.Name, (Action)(() => view.OnDevice(device)))));
        foreach (var (name, step) in steps)
        {
            try
            {
                step();
            }
            catch (Exception e)
            {
                L.Error($"Lỗi khi gắn tab giả lập cho {name}: {e}");
            }
        }
    }

    /// <summary>Đồng hồ 2 s: thời hạn key ở cuối cột menu; 1 s: huy hiệu số lỗi chưa xem của menu Log.</summary>
    private void StartLicenseClock()
    {
        var tick = 0;
        var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        timer.Tick += (_, _) =>
        {
            UpdateLogBadge();
            if (tick++ % 2 == 0) ShowLicense();
        };
        timer.Start();
        ShowLicense();
    }

    private void ShowLicense()
    {
        var info = LicenseKeeper.Current;
        if (info == null) return;
        if (info.Free)
        {
            _license.Set("Bản miễn phí", Theme.Dim);
            return;
        }
        double? left = info.Expires > 0 ? info.Expires - (DateTimeOffset.UtcNow.ToUnixTimeSeconds() + info.ClockOffset) : null;
        if (left <= 0)
        {
            AuthSession.ForceLogout("Key đã hết hạn");
            return;
        }
        string text;
        if (left is not { } seconds) text = "Key: không giới hạn";
        else
        {
            var (days, hours, minutes) = ((int)(seconds / 86400), (int)(seconds % 86400 / 3600), (int)(seconds % 3600 / 60));
            text = "Key còn " + (days > 0 ? $"{days} ngày " : "") + (days > 0 || hours > 0 ? $"{hours} giờ " : "") + $"{minutes} phút";
        }
        _license.Set(text, left < 86400 ? Theme.Warn : Theme.Dim);
    }

    private void UpdateLogBadge()
    {
        if (Views.FirstOrDefault(v => v.Name == "Log") is not { } log) return;
        var unread = DTA.App.Views.Logs.LogCenter.UnreadTotal;
        _nav.SetBadge(log.Name, unread > 0 ? unread.ToString() : "");
    }
}
