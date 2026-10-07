#pragma once
#include <memory>
#include <atomic>
#include <thread>
#include "IExcavationService.hpp"
#include "ExcavationModels.hpp"

namespace dta::features::excavation {

enum class ExcavationBotState : uint8_t {
    STATE_IDLE,
    STATE_LOCATE,
    STATE_TELEPORT_CHEST,
    STATE_DIG_LOOP,
    STATE_CLAIM_REWARD,
    STATE_CHECK_REPAIR
};

class ExcavationBot {
public:
    explicit ExcavationBot(std::shared_ptr<IExcavationService> service);
    ~ExcavationBot();

    void Start();
    void Stop();
    [[nodiscard]] bool IsRunning() const noexcept { return m_running.load(); }

    void SetOptions(const ExcavationOptions& options);
    [[nodiscard]] const ExcavationOptions& GetOptions() const noexcept { return m_options; }
    [[nodiscard]] const ExcavationStats& GetStats() const noexcept { return m_stats; }
    [[nodiscard]] ExcavationBotState GetCurrentState() const noexcept { return m_state; }

    void SetPlayerPosition(const Vector3& pos) { m_playerPos = pos; }

private:
    std::shared_ptr<IExcavationService> m_service;
    ExcavationOptions m_options;
    ExcavationStats m_stats;
    ExcavationBotState m_state{ExcavationBotState::STATE_IDLE};
    Vector3 m_playerPos;

    std::atomic<bool> m_running{false};
    std::jthread m_worker;

    void BotLoop(std::stop_token st);
};

} // namespace dta::features::excavation
