#pragma once
#include <memory>
#include <atomic>
#include <thread>
#include <optional>
#include "IMiningService.hpp"
#include "MiningModels.hpp"

namespace dta::features::mining {

enum class MiningState : uint8_t {
    STATE_IDLE,
    STATE_SCAN,
    STATE_APPROACH,
    STATE_SWING_LOOP,
    STATE_COLLECT_DROPS,
    STATE_CHECK_REPAIR
};

class MiningBot {
public:
    explicit MiningBot(std::shared_ptr<IMiningService> service);
    ~MiningBot();

    void Start();
    void Stop();
    [[nodiscard]] bool IsRunning() const noexcept { return m_running.load(); }

    void SetOptions(const MiningOptions& options);
    [[nodiscard]] const MiningOptions& GetOptions() const noexcept { return m_options; }
    [[nodiscard]] const MiningStats& GetStats() const noexcept { return m_stats; }
    [[nodiscard]] MiningState GetCurrentState() const noexcept { return m_state; }

    void SetPlayerPosition(const Vector3& pos) { m_playerPos = pos; }

private:
    std::shared_ptr<IMiningService> m_service;
    MiningOptions m_options;
    MiningStats m_stats;
    MiningState m_state{MiningState::STATE_IDLE};
    Vector3 m_playerPos;

    std::optional<OreModel> m_targetOre;
    std::atomic<bool> m_running{false};
    std::jthread m_worker;

    void BotLoop(std::stop_token st);
};

} // namespace dta::features::mining
