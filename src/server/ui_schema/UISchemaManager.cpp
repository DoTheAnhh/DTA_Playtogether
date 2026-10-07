#include "UISchemaManager.hpp"

namespace dta::server::ui_schema {

UISchemaManager::UISchemaManager() {
    m_cachedSchema = R"JSON({
  "version": "3.0.0",
  "theme": {
    "accent_color": "#00E5FF",
    "background_color": "#0B0D11",
    "card_color": "#14171F",
    "border_color": "#232834",
    "text_color": "#FFFFFF",
    "text_muted": "#94A3B8",
    "success_color": "#10B981",
    "warning_color": "#F59E0B",
    "danger_color": "#EF4444"
  },
  "menus": [
    {
      "id": "tab_dashboard",
      "title": "Tổng Quan",
      "icon": "[*]",
      "enabled": true,
      "components": [
        { "id": "dash_status", "type": "StatusCard", "title": "TRẠNG THÁI HỆ THỐNG", "text": "Hệ thống C++20 Zero-Tap IL2CPP Main Thread Sẵn Sàng", "status": "online" },
        { "id": "dash_btn_start_all", "type": "Button", "label": "BẮT ĐẦU TẤT CẢ BOT", "variant": "primary" },
        { "id": "dash_btn_stop_all", "type": "Button", "label": "DỪNG TẤT CẢ BOT", "variant": "danger" }
      ]
    },
    {
      "id": "tab_fishing",
      "title": "Câu Cá Siêu Tốc",
      "icon": "[Fish]",
      "enabled": true,
      "components": [
        { "id": "fishing_status", "type": "StatusCard", "title": "TRẠNG THÁI CÂU CÁ", "text": "Đang chờ lệnh câu...", "status": "idle" },
        { "id": "fishing_btn_start", "type": "Button", "label": "BẮT ĐẦU CÂU CÁ", "variant": "primary" },
        { "id": "fishing_btn_stop", "type": "Button", "label": "TẠM DỪNG CÂU", "variant": "danger" },
        { "id": "auto_cast_reel", "type": "Toggle", "label": "Tự động thả & giật cá (Zero-Tap <5ms)", "default": true },
        { "id": "auto_repair", "type": "Toggle", "label": "Tự động sửa cần khi hỏng", "default": true },
        { "id": "after_catch_action", "type": "Dropdown", "label": "Xử lý sau khi câu", "options": ["Bảo quản vào túi", "Bán nhanh tức thì"], "default": "Bảo quản vào túi" },
        { "id": "shadow_filter", "type": "MultiSelect", "label": "Lọc cỡ bóng cá", "options": ["Bóng 1", "Bóng 2", "Bóng 3", "Bóng 4", "Bóng 5", "Bóng 6", "Bóng 7"], "default": ["Bóng 5", "Bóng 6", "Bóng 7"] }
      ]
    },
    {
      "id": "tab_mining",
      "title": "Đập Đá & Quặng",
      "icon": "[Mine]",
      "enabled": true,
      "components": [
        { "id": "mining_status", "type": "StatusCard", "title": "TRẠNG THÁI KHAI KHOÁNG", "text": "Sẵn sàng tìm và đập quặng", "status": "idle" },
        { "id": "mining_btn_start", "type": "Button", "label": "BẮT ĐẦU ĐẬP ĐÁ", "variant": "primary" },
        { "id": "mining_btn_stop", "type": "Button", "label": "TẠM DỪNG", "variant": "danger" },
        { "id": "auto_mine", "type": "Toggle", "label": "Tự động vung cuốc đập đá (Zero-Tap)", "default": true },
        { "id": "auto_repair_pickaxe", "type": "Toggle", "label": "Tự động sửa cuốc khi hỏng", "default": true },
        { "id": "ore_filter", "type": "MultiSelect", "label": "Lọc loại quặng mục tiêu", "options": ["Quặng thường", "Quặng vàng", "Kim cương", "Thiên thạch"], "default": ["Quặng vàng", "Kim cương", "Thiên thạch"] }
      ]
    },
    {
      "id": "tab_insect",
      "title": "Bắt Côn Trùng",
      "icon": "[Bug]",
      "enabled": true,
      "components": [
        { "id": "insect_status", "type": "StatusCard", "title": "TRẠNG THÁI BẮT BỌ", "text": "Sẵn sàng định vị côn trùng", "status": "idle" },
        { "id": "insect_btn_start", "type": "Button", "label": "BẮT ĐẦU BẮT BỌ", "variant": "primary" },
        { "id": "insect_btn_stop", "type": "Button", "label": "TẠM DỪNG", "variant": "danger" },
        { "id": "insect_move_mode", "type": "Segment", "label": "Phương thức tiếp cận", "options": ["Đi bộ lén lút", "Dịch chuyển tức thì"], "default": "Dịch chuyển tức thì" },
        { "id": "auto_swing_net", "type": "Toggle", "label": "Tự động vung vợt bắt bọ (Zero-Tap)", "default": true },
        { "id": "insect_filter", "type": "MultiSelect", "label": "Lọc phẩm cấp bọ", "options": ["Thường", "Hiếm", "Cực hiếm", "Huyền thoại"], "default": ["Hiếm", "Cực hiếm", "Huyền thoại"] }
      ]
    },
    {
      "id": "tab_excavation",
      "title": "Đào Kho Báu",
      "icon": "[Dig]",
      "enabled": true,
      "components": [
        { "id": "excavation_status", "type": "StatusCard", "title": "TRẠNG THÁI ĐÀO BÁU", "text": "Sẵn sàng giải mã radar âm thanh", "status": "idle" },
        { "id": "excavation_btn_start", "type": "Button", "label": "BẮT ĐẦU ĐÀO CỔ VẬT", "variant": "primary" },
        { "id": "excavation_btn_stop", "type": "Button", "label": "TẠM DỪNG", "variant": "danger" },
        { "id": "excavation_move_mode", "type": "Segment", "label": "Phương thức di chuyển", "options": ["Dò quét", "Dịch chuyển điểm bíp"], "default": "Dịch chuyển điểm bíp" },
        { "id": "auto_dig_action", "type": "Toggle", "label": "Tự động đào khi tín hiệu đạt đỉnh (Zero-Tap)", "default": true }
      ]
    },
    {
      "id": "tab_farm",
      "title": "Nông Trại",
      "icon": "[Farm]",
      "enabled": true,
      "components": [
        { "id": "farm_status", "type": "StatusCard", "title": "TRẠNG THÁI NÔNG TRẠI", "text": "Sẵn sàng chăm sóc luống cây cá nhân", "status": "idle" },
        { "id": "farm_btn_start", "type": "Button", "label": "BẮT ĐẦU CHĂM NÔNG TRẠI", "variant": "primary" },
        { "id": "farm_btn_stop", "type": "Button", "label": "TẠM DỪNG", "variant": "danger" },
        { "id": "auto_water", "type": "Toggle", "label": "Tự động tưới nước luống khô", "default": true },
        { "id": "auto_harvest", "type": "Toggle", "label": "Tự động thu hoạch nông sản chín", "default": true }
      ]
    },
    {
      "id": "tab_collect",
      "title": "Thu Thập Vật Phẩm",
      "icon": "[Item]",
      "enabled": true,
      "components": [
        { "id": "collect_status", "type": "StatusCard", "title": "TRẠNG THÁI THU THẬP", "text": "Sẵn sàng nhặt vật phẩm map", "status": "idle" },
        { "id": "collect_btn_start", "type": "Button", "label": "BẮT ĐẦU THU THẬP", "variant": "primary" },
        { "id": "collect_btn_stop", "type": "Button", "label": "TẠM DỪNG", "variant": "danger" },
        { "id": "auto_collect", "type": "Toggle", "label": "Tự động nhặt cành cây, hoa, vỏ sò (Zero-Tap)", "default": true }
      ]
    },
    {
      "id": "tab_teleport",
      "title": "Dịch Chuyển Chung",
      "icon": "[Tele]",
      "enabled": true,
      "components": [
        { "id": "teleport_status", "type": "StatusCard", "title": "HỆ THỐNG DỊCH CHUYỂN TOÀN CẦU", "text": "Tất cả điểm dịch chuyển được đồng bộ trực tiếp từ Server", "status": "online" },
        { "id": "tele_btn_sync", "type": "Button", "label": "ĐỒNG BỘ ĐIỂM TELE TỪ SERVER", "variant": "primary" },
        { "id": "tele_btn_go", "type": "Button", "label": "DỊCH CHUYỂN TỨC THÌ (ZERO-TAP)", "variant": "success" },
        { "id": "tele_btn_save_current", "type": "Button", "label": "LƯU VỊ TRÍ HIỆN TẠI", "variant": "secondary" },
        { "id": "zone_plaza", "type": "Button", "label": "CHUYỂN MAP: KHU TRUNG TÂM (PLAZA - 1)", "variant": "primary" },
        { "id": "zone_downtown", "type": "Button", "label": "CHUYỂN MAP: THỊ TRẤN (DOWNTOWN - 2)", "variant": "primary" },
        { "id": "zone_camping", "type": "Button", "label": "CHUYỂN MAP: KHU CẮM TRẠI (CAMPING - 3)", "variant": "primary" },
        { "id": "zone_resort", "type": "Button", "label": "CHUYỂN MAP: KHU NGHỈ DƯỠNG (RESORT - 4)", "variant": "primary" },
        { "id": "zone_home", "type": "Button", "label": "CHUYỂN MAP: NHÀ RIÊNG (HOME - 10)", "variant": "primary" }
      ]
    },
    {
      "id": "tab_esp",
      "title": "ESP & Radar",
      "icon": "[ESP]",
      "enabled": true,
      "components": [
        { "id": "esp_status", "type": "StatusCard", "title": "RADAR & ESP THỰC THỂ", "text": "Hiển thị xuyên tường trực tiếp", "status": "online" },
        { "id": "esp_fish", "type": "Toggle", "label": "Hiện bóng cá và khoảng cách", "default": true },
        { "id": "esp_ore", "type": "Toggle", "label": "Hiện quặng đá và vị trí khoáng sản", "default": true },
        { "id": "esp_insect", "type": "Toggle", "label": "Hiện côn trùng và tọa độ bay", "default": true },
        { "id": "esp_box", "type": "Toggle", "label": "Hiện rương báu và cổ vật", "default": true }
      ]
    },
    {
      "id": "tab_settings",
      "title": "Cài Đặt & Bản Quyền",
      "icon": "[Gear]",
      "enabled": true,
      "components": [
        { "id": "settings_status", "type": "StatusCard", "title": "QUẢN LÝ BẢN QUYỀN & THIẾT LẬP AN TOÀN", "text": "Quản lý License Key và kết nối VPS", "status": "online" },
        { "id": "key_input", "type": "Entry", "label": "License Key Bản Quyền", "default": "DTA-VIP-2026-KEY" },
        { "id": "key_activate_btn", "type": "Button", "label": "KÍCH HOẠT BẢN QUYỀN (ACTIVATE)", "variant": "success" },
        { "id": "server_url_input", "type": "Entry", "label": "Máy Chủ VPS (URL)", "default": "http://127.0.0.1:28445" },
        { "id": "anti_jitter", "type": "Toggle", "label": "Gaussian Micro-Jitter Humanization (8-28ms)", "default": true },
        { "id": "panic_hotkey", "type": "Toggle", "label": "Phím Tắt Khẩn Cấp F12 (Panic Hotkey - Dừng ngay)", "default": true }
      ]
    }
  ]
})JSON";
}

UISchemaManager& UISchemaManager::Instance() {
    static UISchemaManager instance;
    return instance;
}

std::string UISchemaManager::GetSchemaJson() const {
    return m_cachedSchema;
}

void UISchemaManager::UpdateSchema(const std::string& newSchemaJson) {
    m_cachedSchema = newSchemaJson;
}

} // namespace dta::server::ui_schema
