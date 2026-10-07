#include "FishingBot.hpp"
#include "client/core/logger/AsyncLogger.hpp"

namespace dta::features::fishing {

FishingBot::FishingBot(std::shared_ptr<IFishingService> service)
    : m_service(std::move(service)) {}

FishingBot::~FishingBot() {
    Stop();
}

void FishingBot::SetOptions(const FishingBotOptions& options) {
    m_options = options;
}

void FishingBot::Start() {
    if (m_running.exchange(true)) return;
    DTA_LOG_INFO("FishingBot", "Starting FishingBot Zero-Latency state machine...");
    m_state = BotState::STATE_CASTING;
    m_worker = std::jthread([this](std::stop_token st) {
        BotLoop(st);
    });
}

void FishingBot::Stop() {
    if (!m_running.exchange(false)) return;
    DTA_LOG_INFO("FishingBot", "Stopping FishingBot...");
    if (m_worker.joinable()) {
        m_worker.request_stop();
    }
    m_state = BotState::STATE_IDLE;
}

bool FishingBot::CheckFishFilter(const FishModel& fish) {
    // 0. Free Tier Restriction: Không lọc cá, và KHÔNG câu cá bóng 6-7
    if (m_options.isFreeTier) {
        if (fish.shadowTier >= 6) {
            DTA_LOG_INFO("FishingBot", "[Free Tier] Cá bóng " + std::to_string(fish.shadowTier) +
                " (" + fish.name + ") bị chặn. Bản miễn phí không hỗ trợ câu cá bóng 6-7!");
            return false;
        }
        // Bản miễn phí không lọc cá (chấp nhận toàn bộ bóng 1-5)
        return true;
    }

    // VIP Logic: Bộ lọc thông minh
    // 1. Shadow size check (1-7)
    if (fish.shadowTier >= 1 && fish.shadowTier <= 7) {
        if (!m_options.filter.enabledShadows[fish.shadowTier]) {
            return false;
        }
    }
    // 2. Grade check (1-5)
    if (fish.grade >= 1 && fish.grade <= 5) {
        if (!m_options.filter.enabledGrades[fish.grade]) {
            return false;
        }
    }
    // 3. Variant / Crown override
    if (fish.isVariant && m_options.filter.keepVariants) return true;
    if (fish.isCrown && m_options.filter.keepCrowns) return true;

    // 4. Target ID list check
    if (!m_options.filter.targetFishIds.empty()) {
        return m_options.filter.targetFishIds.contains(fish.fishId);
    }

    return true;
}

void FishingBot::BotLoop(std::stop_token st) {
    while (!st.stop_requested() && m_running.load()) {
        if (!m_service) {
            std::this_thread::sleep_for(std::chrono::milliseconds(50));
            continue;
        }

        FishingState state = m_service->GetFishingState();

        switch (m_state) {
            case BotState::STATE_CASTING: {
                if (state == FishingState::IDLE) {
                    DTA_LOG_INFO("FishingBot", "Casting rod -> OnClick_Button(0)");
                    m_service->CastRod();
                    m_stats.totalCast++;
                    m_state = BotState::STATE_WAITING_BITE;
                    std::this_thread::sleep_for(std::chrono::milliseconds(300));
                } else if (state == FishingState::WAITING_BITE || state == FishingState::SHADOW) {
                    m_state = BotState::STATE_WAITING_BITE;
                }
                break;
            }

            case BotState::STATE_WAITING_BITE: {
                if (state == FishingState::SHADOW) {
                    // Fish is approaching the bobber
                    auto fishInfo = m_service->GetCurrentFishInfo();
                    if (fishInfo.has_value()) {
                        // Kiểm tra bóng 6-7 ở bản miễn phí hoặc bộ lọc VIP
                        if (!CheckFishFilter(*fishInfo)) {
                            DTA_LOG_INFO("FishingBot", "Fish " + fishInfo->name + " (Bóng " +
                                std::to_string(fishInfo->shadowTier) + ") bỏ qua. Thu cần cast lại.");
                            m_service->CastRod(); // Retract rod immediately
                            m_state = BotState::STATE_CASTING;
                            std::this_thread::sleep_for(std::chrono::milliseconds(400));
                            break;
                        }
                    }
                } else if (state == FishingState::BITE || state == FishingState::BIG_DRAG) {
                    // Nếu là bản Free và gặp trạng thái cá to giật kéo (BIG_DRAG / bóng 6-7)
                    if (m_options.isFreeTier && (state == FishingState::BIG_DRAG || !m_options.enableTugHpPull)) {
                        DTA_LOG_INFO("FishingBot", "[Free Tier] Gặp cá to / BIG_DRAG. Bản miễn phí không có cơ chế giật tụt HP cá bóng 6-7!");
                        // Không giật kéo tụt HP cá to ở bản Free
                        std::this_thread::sleep_for(std::chrono::milliseconds(100));
                        break;
                    }

                    // Dấu ! xuất hiện -> Giật cần
                    DTA_LOG_INFO("FishingBot", "BITE detected! Reeling in...");
                    if (m_options.biteReactionDelayMs > 0) {
                        std::this_thread::sleep_for(std::chrono::milliseconds(m_options.biteReactionDelayMs));
                    }
                    m_service->ReelIn();
                    m_state = BotState::STATE_REELING;
                } else if (state == FishingState::RESULT) {
                    m_state = BotState::STATE_HANDLE_RESULT;
                }
                break;
            }

            case BotState::STATE_REELING: {
                if (state == FishingState::RESULT || m_service->IsResultDialogOpen()) {
                    m_state = BotState::STATE_HANDLE_RESULT;
                } else if (state == FishingState::IDLE) {
                    // Missed or finished
                    m_state = BotState::STATE_CASTING;
                } else if (!m_options.isFreeTier && m_options.enableTugHpPull &&
                          (state == FishingState::BIG_PUMPIN || state == FishingState::BIG_DRAG || state == FishingState::BIG_TUG)) {
                    // [VIP ONLY] Cơ chế giật tụt HP cá khủng bóng 6-7
                    m_service->ReelIn();
                    std::this_thread::sleep_for(std::chrono::milliseconds(45));
                }
                break;
            }

            case BotState::STATE_HANDLE_RESULT: {
                auto fishInfo = m_service->GetCurrentFishInfo();
                if (fishInfo) {
                    m_stats.lastFishName = fishInfo->name;
                    if (fishInfo->isCrown) m_stats.crownsCount++;
                    if (fishInfo->grade == 5) m_stats.legendaryCount++;
                }
                m_stats.totalCaught++;

                // Bản miễn phí: BÁN TẤT CẢ CÁ CÂU ĐƯỢC (không giữ cá)
                if (m_options.isFreeTier || m_options.afterCatch == AfterCatchAction::SELL_FAST) {
                    DTA_LOG_INFO("FishingBot", m_options.isFreeTier ? "[Free Tier] Tự động bán tất cả cá câu được..." : "Fast selling fish...");
                    m_service->SellFish();
                } else {
                    DTA_LOG_INFO("FishingBot", "Keeping fish to backpack...");
                    m_service->KeepFish();
                }
                m_state = BotState::STATE_CHECK_REPAIR;
                std::this_thread::sleep_for(std::chrono::milliseconds(200));
                break;
            }

            case BotState::STATE_CHECK_REPAIR: {
                if (m_options.autoRepair && m_service->IsRodBroken()) {
                    DTA_LOG_INFO("FishingBot", "Auto repairing broken fishing rod...");
                    m_service->RepairRod();
                    std::this_thread::sleep_for(std::chrono::milliseconds(250));
                }
                m_state = BotState::STATE_CASTING;
                break;
            }

            default:
                break;
        }

        // 120 FPS scan loop (chu kỳ 8.3ms)
        std::this_thread::sleep_for(std::chrono::milliseconds(8));
    }
}

} // namespace dta::features::fishing
