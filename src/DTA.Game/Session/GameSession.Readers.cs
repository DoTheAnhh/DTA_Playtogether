using DTA.Game.Ui;
using DTA.Game.World;

namespace DTA.Game.Session;

/// <summary>Các reader dùng chung của phiên (tạo khi cần lần đầu, sống suốt phiên).</summary>
public sealed partial class GameSession
{
    private WidgetReader? _widgets;
    private WorldReader? _world;
    private ScreenReader? _ui;
    private DialogButtons? _buttons;
    private Invoke.Invoker? _invoker;

    private Actions.DialogRouter? _dialogs;
    private Actions.ToolActions? _tools;

    private Data.GameTables? _tables;
    private Data.MapObjects? _map;
    private Data.HeadUps? _headUps;
    private Data.HeldTool? _held;

    private Actor.PlayerReader? _player;
    private Actor.CameraReader? _camera;
    private Actor.VehicleReader? _vehicles;
    private Actor.Warp? _warp;
    private Actor.TeleportService? _teleport;
    private World.GroundReader? _ground;

    private Ui.DialogReader? _dialogsOpen;

    /// <summary>Bảng đang mở, bảng kết quả, chữ trên bảng.</summary>
    public Ui.DialogReader Open => _dialogsOpen ??= new Ui.DialogReader(this);

    /// <summary>Nhân vật: tư thế, chuyển động, motor.</summary>
    public Actor.PlayerReader Player => _player ??= new Actor.PlayerReader(this);

    /// <summary>Camera chính, bản đồ đang chơi, cửa.</summary>
    public Actor.CameraReader Camera => _camera ??= new Actor.CameraReader(this);

    /// <summary>Phương tiện đang dùng.</summary>
    public Actor.VehicleReader Vehicles => _vehicles ??= new Actor.VehicleReader(this);

    /// <summary>Dịch chuyển tức thời (ghi motor).</summary>
    public Actor.Warp Warp => _warp ??= new Actor.Warp(this);

    /// <summary>Pipeline dịch chuyển chuẩn (chống độn thổ, đổi bản đồ).</summary>
    public Actor.TeleportService Teleport => _teleport ??= new Actor.TeleportService(this);

    /// <summary>NavMesh + mặt đất chính xác.</summary>
    public World.GroundReader Ground => _ground ??= new World.GroundReader(this);

    /// <summary>Bảng dữ liệu game (vật phẩm, chữ, loại vật thể).</summary>
    public Data.GameTables Tables => _tables ??= new Data.GameTables(this);

    /// <summary>Vật thể bản đồ + côn trùng.</summary>
    public Data.MapObjects Map => _map ??= new Data.MapObjects(this);

    /// <summary>Bong bóng trên đầu vật thể, hội thoại NPC.</summary>
    public Data.HeadUps HeadUps => _headUps ??= new Data.HeadUps(this);

    /// <summary>Dụng cụ đang cầm.</summary>
    public Data.HeldTool Held => _held ??= new Data.HeldTool(this);

    /// <summary>Xử lý mọi bảng game.</summary>
    public Actions.DialogRouter Dialogs => _dialogs ??= new Actions.DialogRouter(this);

    /// <summary>Thao tác dụng cụ cầm tay.</summary>
    public Actions.ToolActions Tools => _tools ??= new Actions.ToolActions(this);

    /// <summary>Gọi hàm game qua hook.</summary>
    public Invoke.Invoker Invoker => _invoker ??= new Invoke.Invoker(this);

    /// <summary>Widget NGUI -> điểm màn hình.</summary>
    public WidgetReader Widgets => _widgets ??= new WidgetReader(this);

    /// <summary>Vị trí 3D object game, bố cục native.</summary>
    public WorldReader World => _world ??= new WorldReader(this);

    /// <summary>Bảng màn chơi, HUD, điểm thành phần giao diện.</summary>
    public ScreenReader Ui => _ui ??= new ScreenReader(this);

    /// <summary>Nút trong bảng đang mở.</summary>
    public DialogButtons Buttons => _buttons ??= new DialogButtons(this);
}
