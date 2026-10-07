#include <iostream>
#include <string>
#include <csignal>
#include <thread>
#include <chrono>

#include "server/database/DatabaseManager.hpp"
#include "server/ui_schema/UISchemaManager.hpp"
#include "server/security/TokenBucketLimiter.hpp"
#include "server/api/AuthHandler.hpp"
#include "server/network/HttpServer.hpp"
#include "server/services/MasterTeleportService.hpp"
#include "server/ui/ServerPanelWindow.hpp"

using namespace dta::server;

static std::atomic<bool> g_serverRunning{true};

void SignalHandler(int signum) {
    std::cout << "\n[!] Received signal " << signum << ". Shutting down server gracefully...\n";
    g_serverRunning.store(false);
}

int main(int argc, char* argv[]) {
    signal(SIGINT, SignalHandler);
    signal(SIGTERM, SignalHandler);

    uint16_t port = 28445;
    bool headless = false;
    for (int i = 1; i < argc; ++i) {
        std::string arg = argv[i];
        if ((arg == "-p" || arg == "--port") && i + 1 < argc) {
            port = static_cast<uint16_t>(std::stoi(argv[++i]));
        } else if (arg == "--headless" || arg == "-c") {
            headless = true;
        }
    }

    if (!headless) {
        HWND hConsole = GetConsoleWindow();
        if (hConsole) {
            ShowWindow(hConsole, SW_HIDE);
        }
    } else {
        if (AttachConsole(ATTACH_PARENT_PROCESS) || AllocConsole()) {
            FILE* fp = nullptr;
            freopen_s(&fp, "CONOUT$", "w", stdout);
            freopen_s(&fp, "CONOUT$", "w", stderr);
            freopen_s(&fp, "CONIN$", "r", stdin);
        }
    }

    std::cout << "====================================================\n";
    std::cout << "  [DTA] PLAYTOGETHER HIGH-PERFORMANCE SERVER v3.0\n";
    std::cout << "  Core: C++20 Native (Thin-Server & Server-Driven UI)\n";
    std::cout << "  Port: " << port << " (HTTP / REST API)\n";
    std::cout << "====================================================\n";

    // 1. Initialize Database
    database::DatabaseManager::Instance().Init();
    std::cout << "[+] Database initialized with VIP license records.\n";

    // 2. Initialize UI Schema
    std::string schema = ui_schema::UISchemaManager::Instance().GetSchemaJson();
    std::cout << "[+] Server-Driven UI Schema loaded (" << schema.size() << " bytes).\n";

    // 3. Initialize Master Teleport Service
    services::MasterTeleportService::Instance().InitDefaults();
    std::cout << "[+] Master Teleport Service initialized with global coordinates.\n";

    // 4. Start HTTP Server
    network::HttpServer server(port);
    if (!server.Start()) {
        std::cerr << "[-] Failed to start HTTP server on port " << port << "!\n";
        return 1;
    }

    std::cout << "[+] Server running actively! Press Ctrl+C or type 'exit' to stop.\n";
    std::cout << "[*] Endpoints ready: /health, /api/activate, /api/check, /api/ui, /api/tele_positions, /api/keys\n\n";

    if (!headless) {
        std::cout << "[+] Launching Master Server Control Panel GUI...\n";
        HINSTANCE hInst = GetModuleHandle(nullptr);
        if (ui::ServerPanelWindow::Instance().Create(hInst, SW_SHOW)) {
            ui::ServerPanelWindow::Instance().RunMessageLoop();
        }
        g_serverRunning.store(false);
    } else {
        // If headless mode, listen for console commands
        std::jthread inputThread([&](std::stop_token st) {
            std::string line;
            while (!st.stop_requested() && g_serverRunning.load()) {
                if (std::getline(std::cin, line)) {
                    if (line == "exit" || line == "quit" || line == "stop") {
                        std::cout << "[*] Exit command received.\n";
                        g_serverRunning.store(false);
                        break;
                    } else if (line == "status") {
                        std::cout << "[*] Server status: ACTIVE on port " << port << "\n";
                    } else if (!line.empty()) {
                        std::cout << "[?] Unknown command '" << line << "'. Commands: status, exit\n";
                    }
                } else {
                    break;
                }
            }
        });

        while (g_serverRunning.load()) {
            std::this_thread::sleep_for(std::chrono::milliseconds(200));
        }
    }

    server.Stop();
    std::cout << "[+] Server shutdown complete. Goodbye!\n";
    return 0;
}
