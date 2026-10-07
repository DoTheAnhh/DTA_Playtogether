#include "TeleportService.hpp"
#include "client/core/memory/OffsetProvider.hpp"
#include "client/core/logger/AsyncLogger.hpp"

namespace dta::features::teleport {

TeleportService::TeleportService(std::shared_ptr<memory::MemoryService> memory,
                                 std::shared_ptr<native::GameActionDispatcher> dispatcher)
    : m_memory(std::move(memory)), m_dispatcher(std::move(dispatcher)) {}

bool TeleportService::TeleportTo(const Vector3& targetPos) {
    if (!m_dispatcher) return false;
    m_currentPos = targetPos;
    return m_dispatcher->SetTransientPosition(m_motorPtr, targetPos);
}

Vector3 TeleportService::GetCurrentPosition() {
    if (m_memory && m_motorPtr != 0) {
        uintptr_t posOffset = m_motorPtr + memory::OffsetProvider::Instance().GetField().motorTransientPosition;
        m_currentPos.x = m_memory->ReadFloat(posOffset + 0);
        m_currentPos.y = m_memory->ReadFloat(posOffset + 4);
        m_currentPos.z = m_memory->ReadFloat(posOffset + 8);
    }
    return m_currentPos;
}

bool TeleportService::SwitchZone(uint32_t targetMapId, int32_t fromType) {
    if (!m_dispatcher) return false;
    DTA_LOG_INFO("Teleport", "Switching zone to MapId=" + std::to_string(targetMapId) + " without NPC/Portal.");
    m_currentMapId = targetMapId;
    return m_dispatcher->ConnectToZoneMove(m_layerSystemPtr, targetMapId, fromType);
}

uint32_t TeleportService::GetCurrentMapId() {
    return m_currentMapId;
}

void TeleportService::SaveWaypoint(const std::string& name, const Vector3& pos, uint32_t mapId) {
    WaypointManager::Instance().AddWaypoint(Waypoint{name, pos, mapId});
    DTA_LOG_INFO("Teleport", "Saved waypoint: " + name + " for map " + std::to_string(mapId));
}

std::vector<Waypoint> TeleportService::GetWaypoints(uint32_t mapId) {
    return WaypointManager::Instance().GetWaypointsByMap(mapId);
}

bool TeleportService::DeleteWaypoint(const std::string& name) {
    return WaypointManager::Instance().RemoveWaypoint(name);
}

} // namespace dta::features::teleport
