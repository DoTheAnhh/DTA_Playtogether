#pragma once
#include <memory>
#include <atomic>
#include <thread>
#include <chrono>
#include "IFishingService.hpp"
#include "FishingModels.hpp"

namespace dta::features::fishing {

enum class BotState : uint8_t {
    STATE_IDLE,
    STATE_CASTING,
    STATE_WAITING_BITE,
    STATE_FISH_APPROACH,
    STATE_BITE_HOOK,
    STATE_REELING,
    STATE_HANDLE_RESULT,
    STATE_CHECK_REPAIR
};

class FishingBot {
public:
    explicit FishingBot(std::shared_ptr<IFishingService> service);
    ~FishingBot();

    void Start();
    void Stop();
    [[nodiscard]] bool IsRunning() const noexcept { return m_running.load(); }

    void SetOptions(const FishingBotOptions& options);
    [[nodiscard]] const FishingBotOptions& GetOptions() const noexcept { return m_options; }
    [[nodiscard]] const FishingStats& GetStats() const noexcept { return m_stats; }
    [[nodiscard]] BotState GetCurrentState() const noexcept { return m_state; }

private:
    std::shared_ptr<IFishingService> m_service;
    FishingBotOptions m_options;
    FishingStats m_stats;
    BotState m_state{BotState::STATE_IDLE};

    std::atomic<bool> m_running{false};
    std::jthread m_worker;

    void BotLoop(std::stop_token st);
    bool CheckFishFilter(const FishModel& fish);
};

} // namespace dta::features::fishing
