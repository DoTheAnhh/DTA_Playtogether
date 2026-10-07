#include <iostream>
#include <memory>
#include <thread>
#include <chrono>

// Core
#include "client/core/logger/AsyncLogger.hpp"
#include "client/core/memory/OffsetProvider.hpp"
#include "client/core/memory/MemoryService.hpp"
#include "client/core/device/DeviceManager.hpp"
#include "client/core/native/GameActionDispatcher.hpp"
#include "client/core/native/PopupInterceptor.hpp"
#include "client/security/AntiDebug.hpp"
#include "client/security/HWIDProvider.hpp"
#include "client/network/NetworkClient.hpp"

// Features
#include "client/features/fishing/FishingCatalog.hpp"
#include "client/features/fishing/FishingService.hpp"
#include "client/features/fishing/FishingBot.hpp"

#include "client/features/mining/MiningService.hpp"
#include "client/features/mining/MiningBot.hpp"

#include "client/features/insect/InsectService.hpp"
#include "client/features/insect/InsectBot.hpp"

#include "client/features/excavation/ExcavationService.hpp"
#include "client/features/excavation/ExcavationBot.hpp"

#include "client/features/farm/FarmService.hpp"
#include "client/features/farm/FarmBot.hpp"

#include "client/features/collect/CollectService.hpp"
#include "client/features/collect/CollectBot.hpp"

#include "client/features/teleport/TeleportService.hpp"
#include "client/features/esp/EspService.hpp"
#include "client/features/settings/SettingsService.hpp"
#include "client/features/settings/HotkeyManager.hpp"

// UI
#include "client/ui/UIRenderer.hpp"
#include "client/ui/views/DashboardView.hpp"
#include "client/ui/views/FishingView.hpp"
#include "client/ui/views/MiningView.hpp"
#include "client/ui/views/InsectView.hpp"
#include "client/ui/views/ExcavationView.hpp"
#include "client/ui/views/FarmView.hpp"
#include "client/ui/views/CollectView.hpp"
#include "client/ui/views/TeleportView.hpp"
#include "client/ui/views/EspView.hpp"
#include "client/ui/ClientWindow.hpp"
#include "client/ui/ActivationDialog.hpp"

using namespace dta;

int main(int argc, char* argv[]) {
    // Hide console window immediately if any was allocated
    HWND hConsole = GetConsoleWindow();
    if (hConsole) {
        ShowWindow(hConsole, SW_HIDE);
    }

    // 1. Initialize High-Performance In-Memory Logger (Zero Disk Log)
    logger::AsyncLogger::Instance().Init("");
    DTA_LOG_INFO("Main", "=== [DTA] PlayTogether Native Client v3.0 Starting ===");

    // 2. Hardware ID Fingerprint
    std::string hwid = security::HWIDProvider::GetHWID();
    DTA_LOG_INFO("Security", "Device HWID: " + hwid);

    // 3. Start Anti-Debug & Security Shield
    security::AntiDebug::Instance().StartProtection();

    // 4. Load Verified RVAs and Offsets
    memory::OffsetProvider::Instance().LoadFromFile("data/offsets.json");

    // 5. Load Fish Catalog
    features::fishing::FishingCatalog::Instance().LoadFromFile("data/fish_catalog.json");

    // 6. Emulator Auto-Detection (LDPlayer 9+, MEmu)
    device::DeviceManager::Instance().AutoDetect();
    auto activeDriver = device::DeviceManager::Instance().GetCurrentDriver();

    // 7. Initialize Memory Engine & Dispatchers
    auto memService = std::make_shared<memory::MemoryService>(activeDriver);
    auto dispatcher = std::shared_ptr<native::GameActionDispatcher>(&native::GameActionDispatcher::Instance(), [](auto*){});
    dispatcher->Init(memService);
    native::PopupInterceptor::Instance().Init(memService);

    // 8. Initialize Features & Services
    auto fishingService = std::make_shared<features::fishing::FishingService>(memService, dispatcher);
    auto fishingBot = std::make_shared<features::fishing::FishingBot>(fishingService);

    auto miningService = std::make_shared<features::mining::MiningService>(memService, dispatcher);
    auto miningBot = std::make_shared<features::mining::MiningBot>(miningService);

    auto insectService = std::make_shared<features::insect::InsectService>(memService, dispatcher);
    auto insectBot = std::make_shared<features::insect::InsectBot>(insectService);

    auto excavationService = std::make_shared<features::excavation::ExcavationService>(memService, dispatcher);
    auto excavationBot = std::make_shared<features::excavation::ExcavationBot>(excavationService);

    auto farmService = std::make_shared<features::farm::FarmService>(memService, dispatcher);
    auto farmBot = std::make_shared<features::farm::FarmBot>(farmService);

    auto collectService = std::make_shared<features::collect::CollectService>(memService, dispatcher);
    auto collectBot = std::make_shared<features::collect::CollectBot>(collectService);

    auto teleportService = std::make_shared<features::teleport::TeleportService>(memService, dispatcher);
    auto espService = std::make_shared<features::esp::EspService>(memService);
    auto settingsService = std::make_shared<features::settings::SettingsService>("data/app.json");

    // 9. Register Global Panic Hotkey (F12)
    features::settings::HotkeyManager::Instance().RegisterHotkey(0x7B, [&]() {
        DTA_LOG_WARN("Security", "PANIC HOTKEY F12 TRIGGERED! Stopping all bot engines...");
        fishingBot->Stop();
        miningBot->Stop();
        insectBot->Stop();
        excavationBot->Stop();
        farmBot->Stop();
        collectBot->Stop();
    });
    features::settings::HotkeyManager::Instance().StartListening();

    // 10. Authenticate with VPS Server (Port 28445)
    network::NetworkClient::Instance().Init("127.0.0.1:28445");

    bool headless = false;
    for (int i = 1; i < argc; ++i) {
        std::string arg = argv[i];
        if (arg == "--headless" || arg == "-c") {
            headless = true;
        }
    }

    std::string activeLicenseKey = "DTA-VIP-2026-KEY";
    bool isFreeTier = false;
    HINSTANCE hInst = GetModuleHandle(nullptr);

    if (!headless) {
        // Show Activation Modal Popup (Xác nhận key hoặc Dùng bản miễn phí)
        bool activated = ui::ActivationDialog::Instance().ShowModal(hInst, activeLicenseKey, isFreeTier);
        if (!activated) {
            DTA_LOG_INFO("Main", "Người dùng đóng cửa sổ kích hoạt hoặc hủy bỏ. Đóng chương trình.");
            return 0;
        }
    }

    if (!isFreeTier) {
        network::NetworkClient::Instance().Authenticate(activeLicenseKey, hwid);
    } else {
        DTA_LOG_INFO("Main", "Khởi động Client ở chế độ BẢN MIỄN PHÍ (Free Tier - Giới hạn Câu Cá).");
    }

    // Thiết lập quyền Free Tier cho UI
    ui::ClientWindow::Instance().SetFreeTier(isFreeTier);

    // 11. Setup Client GUI Window with Dependencies
    ui::ClientWindow::Instance().InitDependencies(
        teleportService, fishingBot, miningBot, insectBot,
        excavationBot, farmBot, collectBot
    );

    DTA_LOG_INFO("Main", "All 10 modules and services registered successfully.");
    DTA_LOG_INFO("Main", "System initialized in Zero-Tap Native Mode.");

    if (!headless) {
        DTA_LOG_INFO("Main", "Launching Native Win32 Client GUI Window...");
        if (ui::ClientWindow::Instance().Create(hInst, SW_SHOW)) {
            ui::ClientWindow::Instance().RunMessageLoop();
        }
    }

    DTA_LOG_INFO("Main", "Client shutdown complete.");
    return 0;
}
