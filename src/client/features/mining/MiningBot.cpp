#include "MiningBot.hpp"
#include "OreScanner.hpp"
#include "client/core/logger/AsyncLogger.hpp"

namespace dta::features::mining {

MiningBot::MiningBot(std::shared_ptr<IMiningService> service)
    : m_service(std::move(service)) {}

MiningBot::~MiningBot() {
    Stop();
}

void MiningBot::SetOptions(const MiningOptions& options) {
    m_options = options;
}

void MiningBot::Start() {
    if (m_running.exchange(true)) return;
    DTA_LOG_INFO("MiningBot", "Starting MiningBot state machine...");
    m_state = MiningState::STATE_SCAN;
    m_worker = std::jthread([this](std::stop_token st) {
        BotLoop(st);
    });
}

void MiningBot::Stop() {
    if (!m_running.exchange(false)) return;
    DTA_LOG_INFO("MiningBot", "Stopping MiningBot...");
    if (m_worker.joinable()) {
        m_worker.request_stop();
    }
    m_state = MiningState::STATE_IDLE;
}

void MiningBot::BotLoop(std::stop_token st) {
    while (!st.stop_requested() && m_running.load()) {
        if (!m_service) {
            std::this_thread::sleep_for(std::chrono::milliseconds(50));
            continue;
        }

        switch (m_state) {
            case MiningState::STATE_SCAN: {
                auto rawOres = m_service->ScanOresAround(m_playerPos, m_options.radius);
                auto sortedOres = OreScanner::FilterAndSortOres(rawOres, m_playerPos, m_options);
                if (!sortedOres.empty()) {
                    m_targetOre = sortedOres.front();
                    DTA_LOG_INFO("MiningBot", "Target acquired: " + m_targetOre->name + " (HP: " + std::to_string(m_targetOre->hp) + ")");
                    m_state = MiningState::STATE_APPROACH;
                } else {
                    std::this_thread::sleep_for(std::chrono::milliseconds(500));
                }
                break;
            }

            case MiningState::STATE_APPROACH: {
                if (m_targetOre.has_value()) {
                    // Approach within 1.5m of the ore
                    Vector3 standPos = m_targetOre->position;
                    standPos.z += 1.2f;
                    m_service->TeleportToOre(standPos);
                    m_playerPos = standPos;
                    m_state = MiningState::STATE_SWING_LOOP;
                    std::this_thread::sleep_for(std::chrono::milliseconds(100));
                } else {
                    m_state = MiningState::STATE_SCAN;
                }
                break;
            }

            case MiningState::STATE_SWING_LOOP: {
                if (!m_targetOre.has_value()) {
                    m_state = MiningState::STATE_SCAN;
                    break;
                }

                DTA_LOG_INFO("MiningBot", "Swinging pickaxe -> OnClick_Button(0)");
                m_service->SwingPickax();
                m_stats.totalSwings++;

                std::this_thread::sleep_for(std::chrono::milliseconds(m_options.swingIntervalMs));

                // Check pickax state or remaining ore hp
                m_targetOre->hp--;
                if (m_targetOre->hp <= 0) {
                    DTA_LOG_INFO("MiningBot", "Ore destroyed! Collecting drops...");
                    m_stats.oresDestroyed++;
                    if (m_targetOre->isPrized) m_stats.prizedOres++;
                    m_state = MiningState::STATE_COLLECT_DROPS;
                }
                break;
            }

            case MiningState::STATE_COLLECT_DROPS: {
                if (m_options.autoCollectDrops && m_targetOre.has_value()) {
                    auto drops = m_service->ScanDroppedItemsAround(m_targetOre->position, 4.0f);
                    for (uint32_t uid : drops) {
                        m_service->PickOreItem(uid);
                        m_stats.itemsCollected++;
                        std::this_thread::sleep_for(std::chrono::milliseconds(20));
                    }
                }
                m_targetOre = std::nullopt;
                m_state = MiningState::STATE_CHECK_REPAIR;
                break;
            }

            case MiningState::STATE_CHECK_REPAIR: {
                if (m_options.autoRepair && m_service->IsPickaxBroken()) {
                    DTA_LOG_INFO("MiningBot", "Repairing pickaxe...");
                    m_service->RepairPickax();
                    std::this_thread::sleep_for(std::chrono::milliseconds(250));
                }
                m_state = MiningState::STATE_SCAN;
                break;
            }

            default:
                break;
        }

        std::this_thread::sleep_for(std::chrono::milliseconds(20));
    }
}

} // namespace dta::features::mining
