#pragma once
#include <atomic>
#include <thread>

namespace dta::security {

class AntiDebug {
public:
    static AntiDebug& Instance();
    void StartProtection();
    void StopProtection();
    [[nodiscard]] bool CheckNow();

private:
    AntiDebug() = default;
    ~AntiDebug();

    std::atomic<bool> m_running{false};
    std::jthread m_worker;
};

} // namespace dta::security
