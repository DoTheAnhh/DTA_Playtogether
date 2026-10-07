#pragma once
#include <memory>
#include <atomic>
#include <thread>
#include "IFarmService.hpp"
#include "FarmModels.hpp"

namespace dta::features::farm {

enum class FarmBotState : uint8_t {
    STATE_IDLE,
    STATE_SCAN,
    STATE_PROCESS_PLOTS
};

class FarmBot {
public:
    explicit FarmBot(std::shared_ptr<IFarmService> service);
    ~FarmBot();

    void Start();
    void Stop();
    [[nodiscard]] bool IsRunning() const noexcept { return m_running.load(); }

    void SetOptions(const FarmOptions& options);
    [[nodiscard]] const FarmOptions& GetOptions() const noexcept { return m_options; }
    [[nodiscard]] const FarmStats& GetStats() const noexcept { return m_stats; }

private:
    std::shared_ptr<IFarmService> m_service;
    FarmOptions m_options;
    FarmStats m_stats;
    FarmBotState m_state{FarmBotState::STATE_IDLE};

    std::atomic<bool> m_running{false};
    std::jthread m_worker;

    void BotLoop(std::stop_token st);
};

} // namespace dta::features::farm
