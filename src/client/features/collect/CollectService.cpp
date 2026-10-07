#include "CollectService.hpp"
#include "client/core/logger/AsyncLogger.hpp"

namespace dta::features::collect {

CollectService::CollectService(std::shared_ptr<memory::MemoryService> memory,
                               std::shared_ptr<native::GameActionDispatcher> dispatcher)
    : m_memory(std::move(memory)), m_dispatcher(std::move(dispatcher)) {}

bool CollectService::PickObject(uint32_t objectUid) {
    if (!m_dispatcher) return false;
    return m_dispatcher->PickFieldObject(m_actorPtr, objectUid);
}

bool CollectService::TeleportToObject(const Vector3& pos) {
    if (!m_dispatcher) return false;
    return m_dispatcher->SetTransientPosition(m_motorPtr, pos);
}

bool CollectService::CloseResultDialog() {
    if (!m_dispatcher) return false;
    m_dispatcher->SkipResultItem(m_resultDialogPtr);
    return m_dispatcher->CloseResultItem(m_resultDialogPtr);
}

std::vector<CollectItemModel> CollectService::ScanFieldObjects(const Vector3& /*playerPos*/, float /*radius*/) {
    return {};
}

} // namespace dta::features::collect
