#pragma once
#include "IFarmService.hpp"
#include "client/core/memory/MemoryService.hpp"
#include "client/core/native/GameActionDispatcher.hpp"
#include <memory>

namespace dta::features::farm {

class FarmService : public IFarmService {
public:
    FarmService(std::shared_ptr<memory::MemoryService> memory,
                std::shared_ptr<native::GameActionDispatcher> dispatcher);

    bool WaterPlot(uint32_t plotUid) override;
    bool HarvestPlot(uint32_t plotUid) override;
    bool PlantSeed(uint32_t plotUid, uint32_t seedId) override;
    bool CloseFarmDialog() override;

    std::vector<FarmPlotModel> ScanAllPlots() override;

    void SetDialogPtr(uintptr_t ptr) { m_dialogPtr = ptr; }

private:
    std::shared_ptr<memory::MemoryService> m_memory;
    std::shared_ptr<native::GameActionDispatcher> m_dispatcher;
    uintptr_t m_dialogPtr{0};
};

} // namespace dta::features::farm
