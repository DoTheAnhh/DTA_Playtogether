#include "InsectService.hpp"
#include "client/core/logger/AsyncLogger.hpp"

namespace dta::features::insect {

InsectService::InsectService(std::shared_ptr<memory::MemoryService> memory,
                             std::shared_ptr<native::GameActionDispatcher> dispatcher)
    : m_memory(std::move(memory)), m_dispatcher(std::move(dispatcher)) {}

bool InsectService::SwingNet() {
    if (!m_dispatcher) return false;
    return m_dispatcher->ClickInsect(m_netPtr);
}

bool InsectService::ApproachInsect(const Vector3& targetPos) {
    if (!m_dispatcher) return false;
    return m_dispatcher->SetTransientPosition(m_motorPtr, targetPos);
}

bool InsectService::FreezeInsect(uint32_t /*insectUid*/) {
    return true;
}

bool InsectService::RepairNet() {
    if (!m_dispatcher) return false;
    return m_dispatcher->RepairItem(m_repairDialogPtr);
}

bool InsectService::CloseResultDialog() {
    if (!m_dispatcher) return false;
    m_dispatcher->SkipResultItem(m_resultDialogPtr);
    return m_dispatcher->CloseResultItem(m_resultDialogPtr);
}

std::vector<InsectModel> InsectService::ScanInsectsAround(const Vector3& /*playerPos*/, float /*radius*/) {
    return {};
}

bool InsectService::IsNetBroken() {
    return false;
}

bool InsectService::IsResultDialogOpen() {
    return m_resultDialogPtr != 0;
}

} // namespace dta::features::insect
