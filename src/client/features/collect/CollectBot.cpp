#include "CollectBot.hpp"
#include "PathOptimizer.hpp"
#include "client/core/logger/AsyncLogger.hpp"

namespace dta::features::collect {

CollectBot::CollectBot(std::shared_ptr<ICollectService> service)
    : m_service(std::move(service)) {}

CollectBot::~CollectBot() {
    Stop();
}

void CollectBot::SetFilter(const CollectFilter& filter) {
    m_filter = filter;
}

void CollectBot::Start() {
    if (m_running.exchange(true)) return;
    DTA_LOG_INFO("CollectBot", "Starting CollectBot automated item gatherer...");
    m_state = CollectBotState::STATE_SCAN_AND_ROUTE;
    m_worker = std::jthread([this](std::stop_token st) {
        BotLoop(st);
    });
}

void CollectBot::Stop() {
    if (!m_running.exchange(false)) return;
    DTA_LOG_INFO("CollectBot", "Stopping CollectBot...");
    if (m_worker.joinable()) {
        m_worker.request_stop();
    }
    m_state = CollectBotState::STATE_IDLE;
}

void CollectBot::BotLoop(std::stop_token st) {
    while (!st.stop_requested() && m_running.load()) {
        if (!m_service) {
            std::this_thread::sleep_for(std::chrono::milliseconds(50));
            continue;
        }

        switch (m_state) {
            case CollectBotState::STATE_SCAN_AND_ROUTE: {
                auto rawItems = m_service->ScanFieldObjects(m_playerPos, m_filter.maxRadius);
                auto optimizedRoute = PathOptimizer::OptimizeRoute(m_playerPos, rawItems);

                if (!optimizedRoute.empty()) {
                    DTA_LOG_INFO("CollectBot", "Optimized route planned with " + std::to_string(optimizedRoute.size()) + " items.");
                    for (const auto& item : optimizedRoute) {
                        if (st.stop_requested()) break;

                        // Teleport to item
                        m_service->TeleportToObject(item.position);
                        m_playerPos = item.position;

                        // Pick object
                        m_service->PickObject(item.uid);
                        m_stats.itemsPicked++;

                        std::this_thread::sleep_for(std::chrono::milliseconds(25));
                        m_service->CloseResultDialog();
                    }
                    m_stats.routeCount++;
                }

                // Check map again after 20 seconds
                std::this_thread::sleep_for(std::chrono::seconds(20));
                break;
            }

            default:
                break;
        }

        std::this_thread::sleep_for(std::chrono::milliseconds(100));
    }
}

} // namespace dta::features::collect
