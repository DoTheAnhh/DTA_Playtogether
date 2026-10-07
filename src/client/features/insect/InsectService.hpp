#pragma once
#include "IInsectService.hpp"
#include "client/core/memory/MemoryService.hpp"
#include "client/core/native/GameActionDispatcher.hpp"
#include <memory>

namespace dta::features::insect {

class InsectService : public IInsectService {
public:
    InsectService(std::shared_ptr<memory::MemoryService> memory,
                  std::shared_ptr<native::GameActionDispatcher> dispatcher);

    bool SwingNet() override;
    bool ApproachInsect(const Vector3& targetPos) override;
    bool FreezeInsect(uint32_t insectUid) override;
    bool RepairNet() override;
    bool CloseResultDialog() override;

    std::vector<InsectModel> ScanInsectsAround(const Vector3& playerPos, float radius) override;
    bool IsNetBroken() override;
    bool IsResultDialogOpen() override;

    void SetNetControllerPtr(uintptr_t ptr) { m_netPtr = ptr; }
    void SetMotorPtr(uintptr_t ptr) { m_motorPtr = ptr; }
    void SetResultDialogPtr(uintptr_t ptr) { m_resultDialogPtr = ptr; }
    void SetRepairDialogPtr(uintptr_t ptr) { m_repairDialogPtr = ptr; }

private:
    std::shared_ptr<memory::MemoryService> m_memory;
    std::shared_ptr<native::GameActionDispatcher> m_dispatcher;

    uintptr_t m_netPtr{0};
    uintptr_t m_motorPtr{0};
    uintptr_t m_resultDialogPtr{0};
    uintptr_t m_repairDialogPtr{0};
};

} // namespace dta::features::insect
