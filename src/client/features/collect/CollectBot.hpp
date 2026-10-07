#pragma once
#include <memory>
#include <atomic>
#include <thread>
#include "ICollectService.hpp"
#include "CollectModels.hpp"

namespace dta::features::collect {

enum class CollectBotState : uint8_t {
    STATE_IDLE,
    STATE_SCAN_AND_ROUTE,
    STATE_EXECUTE_ROUTE
};

class CollectBot {
public:
    explicit CollectBot(std::shared_ptr<ICollectService> service);
    ~CollectBot();

    void Start();
    void Stop();
    [[nodiscard]] bool IsRunning() const noexcept { return m_running.load(); }

    void SetFilter(const CollectFilter& filter);
    [[nodiscard]] const CollectFilter& GetFilter() const noexcept { return m_filter; }
    [[nodiscard]] const CollectStats& GetStats() const noexcept { return m_stats; }

    void SetPlayerPosition(const Vector3& pos) { m_playerPos = pos; }

private:
    std::shared_ptr<ICollectService> m_service;
    CollectFilter m_filter;
    CollectStats m_stats;
    CollectBotState m_state{CollectBotState::STATE_IDLE};
    Vector3 m_playerPos;

    std::atomic<bool> m_running{false};
    std::jthread m_worker;

    void BotLoop(std::stop_token st);
};

} // namespace dta::features::collect
