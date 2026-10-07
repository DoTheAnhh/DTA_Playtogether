#pragma once
#include <memory>
#include <atomic>
#include <thread>
#include <optional>
#include "IInsectService.hpp"
#include "InsectModels.hpp"

namespace dta::features::insect {

enum class InsectBotState : uint8_t {
    STATE_IDLE,
    STATE_SCAN,
    STATE_APPROACH,
    STATE_SWING,
    STATE_CLOSE_RESULT,
    STATE_CHECK_REPAIR
};

class InsectBot {
public:
    explicit InsectBot(std::shared_ptr<IInsectService> service);
    ~InsectBot();

    void Start();
    void Stop();
    [[nodiscard]] bool IsRunning() const noexcept { return m_running.load(); }

    void SetOptions(const InsectOptions& options);
    [[nodiscard]] const InsectOptions& GetOptions() const noexcept { return m_options; }
    [[nodiscard]] const InsectStats& GetStats() const noexcept { return m_stats; }
    [[nodiscard]] InsectBotState GetCurrentState() const noexcept { return m_state; }

    void SetPlayerPosition(const Vector3& pos) { m_playerPos = pos; }

private:
    std::shared_ptr<IInsectService> m_service;
    InsectOptions m_options;
    InsectStats m_stats;
    InsectBotState m_state{InsectBotState::STATE_IDLE};
    Vector3 m_playerPos;

    std::optional<InsectModel> m_targetInsect;
    std::atomic<bool> m_running{false};
    std::jthread m_worker;

    void BotLoop(std::stop_token st);
};

} // namespace dta::features::insect
