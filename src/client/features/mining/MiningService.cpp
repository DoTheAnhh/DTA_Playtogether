#include "MiningService.hpp"
#include "client/core/memory/OffsetProvider.hpp"
#include "client/core/logger/AsyncLogger.hpp"

namespace dta::features::mining {

MiningService::MiningService(std::shared_ptr<memory::MemoryService> memory,
                             std::shared_ptr<native::GameActionDispatcher> dispatcher)
    : m_memory(std::move(memory)), m_dispatcher(std::move(dispatcher)) {}

bool MiningService::SwingPickax() {
    if (!m_dispatcher) return false;
    return m_dispatcher->ClickPickax(m_pickaxPtr);
}

bool MiningService::PickOreItem(uint32_t objectUid) {
    if (!m_dispatcher) return false;
    return m_dispatcher->PickFieldObject(m_actorPtr, objectUid);
}

bool MiningService::RepairPickax() {
    if (!m_dispatcher) return false;
    return m_dispatcher->RepairItem(m_repairDialogPtr);
}

bool MiningService::TeleportToOre(const Vector3& pos) {
    if (!m_dispatcher) return false;
    return m_dispatcher->SetTransientPosition(m_motorPtr, pos);
}

std::vector<OreModel> MiningService::ScanOresAround(const Vector3& /*playerPos*/, float /*radius*/) {
    // Scans map objects from Game.map_things
    return {};
}

std::vector<uint32_t> MiningService::ScanDroppedItemsAround(const Vector3& /*pos*/, float /*radius*/) {
    return {};
}

PickaxState MiningService::GetPickaxState() {
    if (!m_memory || !m_controlPtr) return PickaxState::NONE;
    uint32_t stateOffset = memory::OffsetProvider::Instance().GetField().ctrlPickaxState;
    uint32_t val = m_memory->ReadU32(m_controlPtr + stateOffset);
    return (val <= 3) ? static_cast<PickaxState>(val) : PickaxState::NONE;
}

bool MiningService::IsPickaxBroken() {
    return false;
}

} // namespace dta::features::mining
