#include "FarmService.hpp"
#include "client/core/logger/AsyncLogger.hpp"

namespace dta::features::farm {

FarmService::FarmService(std::shared_ptr<memory::MemoryService> memory,
                         std::shared_ptr<native::GameActionDispatcher> dispatcher)
    : m_memory(std::move(memory)), m_dispatcher(std::move(dispatcher)) {}

bool FarmService::WaterPlot(uint32_t plotUid) {
    DTA_LOG_INFO("Farm", "Watering plot uid=" + std::to_string(plotUid));
    return true;
}

bool FarmService::HarvestPlot(uint32_t plotUid) {
    DTA_LOG_INFO("Farm", "Harvesting plot uid=" + std::to_string(plotUid));
    return true;
}

bool FarmService::PlantSeed(uint32_t plotUid, uint32_t seedId) {
    DTA_LOG_INFO("Farm", "Planting seed id=" + std::to_string(seedId) + " into plot uid=" + std::to_string(plotUid));
    return true;
}

bool FarmService::CloseFarmDialog() {
    if (!m_dispatcher) return false;
    return m_dispatcher->CloseResultItem(m_dialogPtr);
}

std::vector<FarmPlotModel> FarmService::ScanAllPlots() {
    return {};
}

} // namespace dta::features::farm
