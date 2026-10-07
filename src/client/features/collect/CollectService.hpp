#pragma once
#include "ICollectService.hpp"
#include "client/core/memory/MemoryService.hpp"
#include "client/core/native/GameActionDispatcher.hpp"
#include <memory>

namespace dta::features::collect {

class CollectService : public ICollectService {
public:
    CollectService(std::shared_ptr<memory::MemoryService> memory,
                   std::shared_ptr<native::GameActionDispatcher> dispatcher);

    bool PickObject(uint32_t objectUid) override;
    bool TeleportToObject(const Vector3& pos) override;
    bool CloseResultDialog() override;

    std::vector<CollectItemModel> ScanFieldObjects(const Vector3& playerPos, float radius) override;

    void SetActorPtr(uintptr_t ptr) { m_actorPtr = ptr; }
    void SetMotorPtr(uintptr_t ptr) { m_motorPtr = ptr; }
    void SetResultDialogPtr(uintptr_t ptr) { m_resultDialogPtr = ptr; }

private:
    std::shared_ptr<memory::MemoryService> m_memory;
    std::shared_ptr<native::GameActionDispatcher> m_dispatcher;

    uintptr_t m_actorPtr{0};
    uintptr_t m_motorPtr{0};
    uintptr_t m_resultDialogPtr{0};
};

} // namespace dta::features::collect
