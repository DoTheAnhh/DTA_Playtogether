#pragma once
#include "IFishingService.hpp"
#include "client/core/memory/MemoryService.hpp"
#include "client/core/native/GameActionDispatcher.hpp"
#include "FishingCatalog.hpp"
#include <memory>

namespace dta::features::fishing {

class FishingService : public IFishingService {
public:
    FishingService(std::shared_ptr<memory::MemoryService> memory,
                   std::shared_ptr<native::GameActionDispatcher> dispatcher);

    bool CastRod() override;
    bool ReelIn() override;
    bool KeepFish() override;
    bool SellFish() override;
    bool OpenBox() override;
    bool RepairRod() override;

    FishingState GetFishingState() override;
    std::optional<FishModel> GetCurrentFishInfo() override;
    bool IsRodBroken() override;
    bool IsResultDialogOpen() override;

    void SetRodControllerPtr(uintptr_t ptr) { m_rodPtr = ptr; }
    void SetDialogPtr(uintptr_t ptr) { m_dialogPtr = ptr; }
    void SetControlPtr(uintptr_t ptr) { m_controlPtr = ptr; }

private:
    std::shared_ptr<memory::MemoryService> m_memory;
    std::shared_ptr<native::GameActionDispatcher> m_dispatcher;

    uintptr_t m_rodPtr{0};
    uintptr_t m_dialogPtr{0};
    uintptr_t m_controlPtr{0};
    uintptr_t m_storageDicPtr{0};
};

} // namespace dta::features::fishing
