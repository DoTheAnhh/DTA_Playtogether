#include "InsectBot.hpp"
#include "InsectPredictor.hpp"
#include "client/core/logger/AsyncLogger.hpp"

namespace dta::features::insect {

InsectBot::InsectBot(std::shared_ptr<IInsectService> service)
    : m_service(std::move(service)) {}

InsectBot::~InsectBot() {
    Stop();
}

void InsectBot::SetOptions(const InsectOptions& options) {
    m_options = options;
}

void InsectBot::Start() {
    if (m_running.exchange(true)) return;
    DTA_LOG_INFO("InsectBot", "Starting InsectBot state machine...");
    m_state = InsectBotState::STATE_SCAN;
    m_worker = std::jthread([this](std::stop_token st) {
        BotLoop(st);
    });
}

void InsectBot::Stop() {
    if (!m_running.exchange(false)) return;
    DTA_LOG_INFO("InsectBot", "Stopping InsectBot...");
    if (m_worker.joinable()) {
        m_worker.request_stop();
    }
    m_state = InsectBotState::STATE_IDLE;
}

void InsectBot::BotLoop(std::stop_token st) {
    while (!st.stop_requested() && m_running.load()) {
        if (!m_service) {
            std::this_thread::sleep_for(std::chrono::milliseconds(50));
            continue;
        }

        switch (m_state) {
            case InsectBotState::STATE_SCAN: {
                auto list = m_service->ScanInsectsAround(m_playerPos, m_options.radius);
                if (!list.empty()) {
                    m_targetInsect = list.front();
                    DTA_LOG_INFO("InsectBot", "Target insect acquired: " + m_targetInsect->name);
                    m_state = InsectBotState::STATE_APPROACH;
                } else {
                    std::this_thread::sleep_for(std::chrono::milliseconds(300));
                }
                break;
            }

            case InsectBotState::STATE_APPROACH: {
                if (m_targetInsect.has_value()) {
                    // Predict trajectory
                    Vector3 predicted = InsectPredictor::PredictPosition(
                        m_targetInsect->position,
                        m_targetInsect->velocity,
                        m_options.swingLatencySeconds);

                    Vector3 approach = InsectPredictor::CalculateApproachPosition(predicted, m_targetInsect->velocity);
                    m_service->ApproachInsect(approach);
                    m_playerPos = approach;
                    m_state = InsectBotState::STATE_SWING;
                    std::this_thread::sleep_for(std::chrono::milliseconds(80));
                } else {
                    m_state = InsectBotState::STATE_SCAN;
                }
                break;
            }

            case InsectBotState::STATE_SWING: {
                DTA_LOG_INFO("InsectBot", "Swinging insect net -> OnClick_Button(0)");
                m_service->SwingNet();
                m_stats.totalSwings++;

                std::this_thread::sleep_for(std::chrono::milliseconds(250));
                m_state = InsectBotState::STATE_CLOSE_RESULT;
                break;
            }

            case InsectBotState::STATE_CLOSE_RESULT: {
                if (m_targetInsect.has_value()) {
                    m_stats.insectsCaught++;
                    m_stats.lastInsectName = m_targetInsect->name;
                    if (m_targetInsect->isCrown) m_stats.crownsCaught++;
                }

                DTA_LOG_INFO("InsectBot", "Closing catch result dialog...");
                m_service->CloseResultDialog();
                m_targetInsect = std::nullopt;
                m_state = InsectBotState::STATE_CHECK_REPAIR;
                std::this_thread::sleep_for(std::chrono::milliseconds(200));
                break;
            }

            case InsectBotState::STATE_CHECK_REPAIR: {
                if (m_options.autoRepair && m_service->IsNetBroken()) {
                    DTA_LOG_INFO("InsectBot", "Repairing net...");
                    m_service->RepairNet();
                    std::this_thread::sleep_for(std::chrono::milliseconds(250));
                }
                m_state = InsectBotState::STATE_SCAN;
                break;
            }

            default:
                break;
        }

        std::this_thread::sleep_for(std::chrono::milliseconds(20));
    }
}

} // namespace dta::features::insect
