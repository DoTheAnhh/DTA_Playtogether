#include "WaypointManager.hpp"
#include <algorithm>

namespace dta::features::teleport {

WaypointManager::WaypointManager() {
    InitDefaults();
}

WaypointManager& WaypointManager::Instance() {
    static WaypointManager instance;
    return instance;
}

void WaypointManager::InitDefaults() {
    m_waypoints.clear();
    // Default verified map spots
    m_waypoints.push_back(Waypoint{"Hồ Plaza (Cá lớn)", Vector3(-12.4f, 0.5f, 45.2f), 1});
    m_waypoints.push_back(Waypoint{"Bến cảng Plaza", Vector3(34.8f, 1.2f, -88.5f), 1});
    m_waypoints.push_back(Waypoint{"Mỏ quặng Downtown", Vector3(112.5f, -5.0f, 64.3f), 2});
    m_waypoints.push_back(Waypoint{"Suối Camping", Vector3(-45.0f, 12.0f, 130.5f), 3});
    m_waypoints.push_back(Waypoint{"Vườn nhà Home", Vector3(0.0f, 0.0f, 5.0f), 10});
}

void WaypointManager::AddWaypoint(const Waypoint& wp) {
    RemoveWaypoint(wp.name);
    m_waypoints.push_back(wp);
}

bool WaypointManager::RemoveWaypoint(const std::string& name) {
    auto it = std::remove_if(m_waypoints.begin(), m_waypoints.end(),
        [&](const Waypoint& w) { return w.name == name; });
    if (it != m_waypoints.end()) {
        m_waypoints.erase(it, m_waypoints.end());
        return true;
    }
    return false;
}

std::vector<Waypoint> WaypointManager::GetWaypointsByMap(uint32_t mapId) const {
    std::vector<Waypoint> res;
    for (const auto& wp : m_waypoints) {
        if (wp.mapId == mapId) {
            res.push_back(wp);
        }
    }
    return res;
}

} // namespace dta::features::teleport
