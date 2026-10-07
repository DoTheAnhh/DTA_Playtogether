#pragma once
#include <functional>
#include <unordered_map>
#include <atomic>
#include <thread>

namespace dta::features::settings {

class HotkeyManager {
public:
    static HotkeyManager& Instance();

    void RegisterHotkey(int vkCode, std::function<void()> callback);
    void StartListening();
    void StopListening();

private:
    HotkeyManager() = default;
    ~HotkeyManager();

    std::unordered_map<int, std::function<void()>> m_hotkeys;
    std::atomic<bool> m_running{false};
    std::jthread m_worker;
};

} // namespace dta::features::settings
