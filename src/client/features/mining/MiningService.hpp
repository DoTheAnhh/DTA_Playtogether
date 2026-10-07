#pragma once
#include "IMiningService.hpp"
#include "client/core/memory/MemoryService.hpp"
#include "client/core/native/GameActionDispatcher.hpp"
#include <memory>

namespace dta::features::mining {

class MiningService : public IMiningService {
public:
    MiningService(std::shared_ptr<memory::MemoryService> memory,
                  std::shared_ptr<native::GameActionDispatcher> dispatcher);

    bool SwingPickax() override;
    bool PickOreItem(uint32_t objectUid) override;
    bool RepairPickax() override;
    bool TeleportToOre(const Vector3& pos) override;

    std::vector<OreModel> ScanOresAround(const Vector3& playerPos, float radius) override;
    std::vector<uint32_t> ScanDroppedItemsAround(const Vector3& pos, float radius) override;
    PickaxState GetPickaxState() override;
    bool IsPickaxBroken() override;

    void SetPickaxControllerPtr(uintptr_t ptr) { m_pickaxPtr = ptr; }
    void SetMotorPtr(uintptr_t ptr) { m_motorPtr = ptr; }
    void SetActorPtr(uintptr_t ptr) { m_actorPtr = ptr; }
    void SetControlPtr(uintptr_t ptr) { m_controlPtr = ptr; }
    void SetRepairDialogPtr(uintptr_t ptr) { m_repairDialogPtr = ptr; }

private:
    std::shared_ptr<memory::MemoryService> m_memory;
    std::shared_ptr<native::GameActionDispatcher> m_dispatcher;

    uintptr_t m_pickaxPtr{0};
    uintptr_t m_motorPtr{0};
    uintptr_t m_actorPtr{0};
    uintptr_t m_controlPtr{0};
    uintptr_t m_repairDialogPtr{0};
};

} // namespace dta::features::mining
