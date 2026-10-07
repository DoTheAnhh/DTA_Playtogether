#pragma once
#include "IExcavationService.hpp"
#include "client/core/memory/MemoryService.hpp"
#include "client/core/native/GameActionDispatcher.hpp"
#include <memory>

namespace dta::features::excavation {

class ExcavationService : public IExcavationService {
public:
    ExcavationService(std::shared_ptr<memory::MemoryService> memory,
                      std::shared_ptr<native::GameActionDispatcher> dispatcher);

    bool DigShovel() override;
    bool TeleportToPoint(const Vector3& pos) override;
    bool OpenTreasureChest() override;
    bool RepairShovel() override;
    bool CloseRewardDialog() override;

    float ReadCurrentRadarSignal() override;
    std::vector<RelicModel> ScanExcavationSpots() override;
    bool IsShovelBroken() override;
    bool IsChestOpen() override;

    void SetShovelPtr(uintptr_t ptr) { m_shovelPtr = ptr; }
    void SetMotorPtr(uintptr_t ptr) { m_motorPtr = ptr; }
    void SetRewardDialogPtr(uintptr_t ptr) { m_rewardDialogPtr = ptr; }
    void SetRepairDialogPtr(uintptr_t ptr) { m_repairDialogPtr = ptr; }

private:
    std::shared_ptr<memory::MemoryService> m_memory;
    std::shared_ptr<native::GameActionDispatcher> m_dispatcher;

    uintptr_t m_shovelPtr{0};
    uintptr_t m_motorPtr{0};
    uintptr_t m_rewardDialogPtr{0};
    uintptr_t m_repairDialogPtr{0};
};

} // namespace dta::features::excavation
