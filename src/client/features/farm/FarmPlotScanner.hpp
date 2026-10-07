#pragma once
#include <vector>
#include "FarmModels.hpp"

namespace dta::features::farm {

class FarmPlotScanner {
public:
    static std::vector<FarmPlotModel> ClassifyPlots(const std::vector<FarmPlotModel>& allPlots);
};

} // namespace dta::features::farm
