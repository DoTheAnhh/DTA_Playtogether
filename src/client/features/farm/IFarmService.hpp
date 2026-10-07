#pragma once
#include <vector>
#include <cstdint>
#include "FarmModels.hpp"

namespace dta::features::farm {

class IFarmService {
public:
    virtual ~IFarmService() = default;

    virtual bool WaterPlot(uint32_t plotUid) = 0;
    virtual bool HarvestPlot(uint32_t plotUid) = 0;
    virtual bool PlantSeed(uint32_t plotUid, uint32_t seedId) = 0;
    virtual bool CloseFarmDialog() = 0;

    virtual std::vector<FarmPlotModel> ScanAllPlots() = 0;
};

} // namespace dta::features::farm
