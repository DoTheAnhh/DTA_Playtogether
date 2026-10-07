#include "ExcavationBot.hpp"
#include "RadarSolver.hpp"
#include "client/core/logger/AsyncLogger.hpp"

namespace dta::features::excavation {

ExcavationBot::ExcavationBot(std::shared_ptr<IExcavationService> service)
    : m_service(std::move(service)) {}

ExcavationBot::~ExcavationBot() {
    Stop();
}

void ExcavationBot::SetOptions(const ExcavationOptions& options) {
    m_options = options;
}

void ExcavationBot::Start() {
    if (m_running.exchange(true)) return;
    DTA_LOG_INFO("ExcavationBot", "Starting ExcavationBot state machine...");
    m_state = ExcavationBotState::STATE_LOCATE;
    m_worker = std::jthread([this](std::stop_token st) {
        BotLoop(st);
    });
}

void ExcavationBot::Stop() {
    if (!m_running.exchange(false)) return;
    DTA_LOG_INFO("ExcavationBot", "Stopping ExcavationBot...");
    if (m_worker.joinable()) {
        m_worker.request_stop();
    }
    m_state = ExcavationBotState::STATE_IDLE;
}

void ExcavationBot::BotLoop(std::stop_token st) {
    while (!st.stop_requested() && m_running.load()) {
        if (!m_service) {
            std::this_thread::sleep_for(std::chrono::milliseconds(50));
            continue;
        }

        switch (m_state) {
            case ExcavationBotState::STATE_LOCATE: {
                DTA_LOG_INFO("ExcavationBot", "Locating buried chest via triangulation...");
                // 3 sample points
                RadarSample s1{m_playerPos, m_service->ReadCurrentRadarSignal()};
                RadarSample s2{Vector3(m_playerPos.x + 8.0f, m_playerPos.y, m_playerPos.z), 0.65f};
                RadarSample s3{Vector3(m_playerPos.x, m_playerPos.y, m_playerPos.z + 8.0f), 0.72f};

                auto chestPos = RadarSolver::TriangulateChest(s1, s2, s3);
                if (chestPos.has_value()) {
                    DTA_LOG_INFO("ExcavationBot", "Chest location calculated at (" +
                        std::to_string(chestPos->x) + ", " + std::to_string(chestPos->z) + ")");
                    m_service->TeleportToPoint(*chestPos);
                    m_playerPos = *chestPos;
                    m_state = ExcavationBotState::STATE_DIG_LOOP;
                } else {
                    std::this_thread::sleep_for(std::chrono::milliseconds(400));
                }
                break;
            }

            case ExcavationBotState::STATE_DIG_LOOP: {
                DTA_LOG_INFO("ExcavationBot", "Digging shovel -> OnClick_Button(0)");
                m_service->DigShovel();
                m_stats.totalDigs++;

                std::this_thread::sleep_for(std::chrono::milliseconds(m_options.digIntervalMs));

                if (m_service->IsChestOpen()) {
                    DTA_LOG_INFO("ExcavationBot", "Chest unburied! Claiming contents...");
                    m_stats.chestsFound++;
                    m_state = ExcavationBotState::STATE_CLAIM_REWARD;
                }
                break;
            }

            case ExcavationBotState::STATE_CLAIM_REWARD: {
                m_service->OpenTreasureChest();
                std::this_thread::sleep_for(std::chrono::milliseconds(200));
                m_service->CloseRewardDialog();
                m_stats.artifactsCollected++;

                m_state = ExcavationBotState::STATE_CHECK_REPAIR;
                break;
            }

            case ExcavationBotState::STATE_CHECK_REPAIR: {
                if (m_options.autoRepair && m_service->IsShovelBroken()) {
                    DTA_LOG_INFO("ExcavationBot", "Repairing shovel...");
                    m_service->RepairShovel();
                    std::this_thread::sleep_for(std::chrono::milliseconds(250));
                }
                m_state = ExcavationBotState::STATE_LOCATE;
                break;
            }

            default:
                break;
        }

        std::this_thread::sleep_for(std::chrono::milliseconds(30));
    }
}

} // namespace dta::features::excavation
