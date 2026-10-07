#include "FarmPlotScanner.hpp"

namespace dta::features::farm {

std::vector<FarmPlotModel> FarmPlotScanner::ClassifyPlots(const std::vector<FarmPlotModel>& allPlots) {
    std::vector<FarmPlotModel> actionable;
    actionable.reserve(allPlots.size());

    for (const auto& plot : allPlots) {
        if (plot.state == FarmPlotState::RIPE_CAN_HARVEST ||
            plot.state == FarmPlotState::NEED_WATER ||
            plot.state == FarmPlotState::EMPTY) {
            actionable.push_back(plot);
        }
    }

    return actionable;
}

} // namespace dta::features::farm
