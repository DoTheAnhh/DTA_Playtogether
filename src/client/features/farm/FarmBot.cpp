#include "FarmBot.hpp"
#include "FarmPlotScanner.hpp"
#include "client/core/logger/AsyncLogger.hpp"

namespace dta::features::farm {

FarmBot::FarmBot(std::shared_ptr<IFarmService> service)
    : m_service(std::move(service)) {}

FarmBot::~FarmBot() {
    Stop();
}

void FarmBot::SetOptions(const FarmOptions& options) {
    m_options = options;
}

void FarmBot::Start() {
    if (m_running.exchange(true)) return;
    DTA_LOG_INFO("FarmBot", "Starting FarmBot automated garden manager...");
    m_state = FarmBotState::STATE_SCAN;
    m_worker = std::jthread([this](std::stop_token st) {
        BotLoop(st);
    });
}

void FarmBot::Stop() {
    if (!m_running.exchange(false)) return;
    DTA_LOG_INFO("FarmBot", "Stopping FarmBot...");
    if (m_worker.joinable()) {
        m_worker.request_stop();
    }
    m_state = FarmBotState::STATE_IDLE;
}

void FarmBot::BotLoop(std::stop_token st) {
    while (!st.stop_requested() && m_running.load()) {
        if (!m_service) {
            std::this_thread::sleep_for(std::chrono::milliseconds(50));
            continue;
        }

        switch (m_state) {
            case FarmBotState::STATE_SCAN: {
                auto allPlots = m_service->ScanAllPlots();
                auto actionable = FarmPlotScanner::ClassifyPlots(allPlots);

                for (const auto& plot : actionable) {
                    if (st.stop_requested()) break;

                    if (plot.state == FarmPlotState::RIPE_CAN_HARVEST && m_options.autoHarvest) {
                        m_service->HarvestPlot(plot.uid);
                        m_stats.totalHarvested++;
                    } else if (plot.state == FarmPlotState::NEED_WATER && m_options.autoWater) {
                        m_service->WaterPlot(plot.uid);
                        m_stats.totalWatered++;
                    } else if (plot.state == FarmPlotState::EMPTY && m_options.autoPlant) {
                        m_service->PlantSeed(plot.uid, m_options.defaultSeedId);
                        m_stats.totalPlanted++;
                    }

                    // Sequential Main Thread batch execution (5ms)
                    std::this_thread::sleep_for(std::chrono::milliseconds(5));
                }

                m_service->CloseFarmDialog();
                // Check garden again after 15 seconds
                std::this_thread::sleep_for(std::chrono::seconds(15));
                break;
            }

            default:
                break;
        }

        std::this_thread::sleep_for(std::chrono::milliseconds(100));
    }
}

} // namespace dta::features::farm
