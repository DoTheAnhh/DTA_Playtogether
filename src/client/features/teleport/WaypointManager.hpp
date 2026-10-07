#pragma once
#include <vector>
#include <string>
#include <unordered_map>
#include "shared/FeatureModels.hpp"

namespace dta::features::teleport {

class WaypointManager {
public:
    static WaypointManager& Instance();

    void InitDefaults();
    void AddWaypoint(const Waypoint& wp);
    bool RemoveWaypoint(const std::string& name);
    [[nodiscard]] std::vector<Waypoint> GetWaypointsByMap(uint32_t mapId) const;
    [[nodiscard]] const std::vector<Waypoint>& GetAllWaypoints() const { return m_waypoints; }

private:
    WaypointManager();
    std::vector<Waypoint> m_waypoints;
};

} // namespace dta::features::teleport
