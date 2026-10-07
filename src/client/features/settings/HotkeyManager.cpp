#include "HotkeyManager.hpp"
#include <windows.h>
#include <chrono>

namespace dta::features::settings {

HotkeyManager& HotkeyManager::Instance() {
    static HotkeyManager instance;
    return instance;
}

HotkeyManager::~HotkeyManager() {
    StopListening();
}

void HotkeyManager::RegisterHotkey(int vkCode, std::function<void()> callback) {
    m_hotkeys[vkCode] = std::move(callback);
}

void HotkeyManager::StartListening() {
    if (m_running.exchange(true)) return;

    m_worker = std::jthread([this](std::stop_token st) {
        while (!st.stop_requested() && m_running.load()) {
            for (const auto& [vk, cb] : m_hotkeys) {
                if (GetAsyncKeyState(vk) & 0x8000) {
                    if (cb) cb();
                    std::this_thread::sleep_for(std::chrono::milliseconds(300)); // debounce
                }
            }
            std::this_thread::sleep_for(std::chrono::milliseconds(20));
        }
    });
}

void HotkeyManager::StopListening() {
    m_running.store(false);
    if (m_worker.joinable()) {
        m_worker.request_stop();
    }
}

} // namespace dta::features::settings
