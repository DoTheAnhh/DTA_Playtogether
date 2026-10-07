#include "ExcavationService.hpp"
#include "client/core/logger/AsyncLogger.hpp"

namespace dta::features::excavation {

ExcavationService::ExcavationService(std::shared_ptr<memory::MemoryService> memory,
                                     std::shared_ptr<native::GameActionDispatcher> dispatcher)
    : m_memory(std::move(memory)), m_dispatcher(std::move(dispatcher)) {}

bool ExcavationService::DigShovel() {
    if (!m_dispatcher) return false;
    return m_dispatcher->ClickShovel(m_shovelPtr);
}

bool ExcavationService::TeleportToPoint(const Vector3& pos) {
    if (!m_dispatcher) return false;
    return m_dispatcher->SetTransientPosition(m_motorPtr, pos);
}

bool ExcavationService::OpenTreasureChest() {
    if (!m_dispatcher) return false;
    return m_dispatcher->ConfirmOk(m_rewardDialogPtr);
}

bool ExcavationService::RepairShovel() {
    if (!m_dispatcher) return false;
    return m_dispatcher->RepairItem(m_repairDialogPtr);
}

bool ExcavationService::CloseRewardDialog() {
    if (!m_dispatcher) return false;
    return m_dispatcher->CloseResultItem(m_rewardDialogPtr);
}

float ExcavationService::ReadCurrentRadarSignal() {
    return 0.5f;
}

std::vector<RelicModel> ExcavationService::ScanExcavationSpots() {
    return {};
}

bool ExcavationService::IsShovelBroken() {
    return false;
}

bool ExcavationService::IsChestOpen() {
    return false;
}

} // namespace dta::features::excavation
