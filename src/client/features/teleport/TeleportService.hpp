#pragma once
#include "ITeleportService.hpp"
#include "client/core/memory/MemoryService.hpp"
#include "client/core/native/GameActionDispatcher.hpp"
#include "WaypointManager.hpp"
#include <memory>

namespace dta::features::teleport {

class TeleportService : public ITeleportService {
public:
    TeleportService(std::shared_ptr<memory::MemoryService> memory,
                    std::shared_ptr<native::GameActionDispatcher> dispatcher);

    bool TeleportTo(const Vector3& targetPos) override;
    Vector3 GetCurrentPosition() override;

    bool SwitchZone(uint32_t targetMapId, int32_t fromType = 10) override;
    uint32_t GetCurrentMapId() override;

    void SaveWaypoint(const std::string& name, const Vector3& pos, uint32_t mapId) override;
    std::vector<Waypoint> GetWaypoints(uint32_t mapId) override;
    bool DeleteWaypoint(const std::string& name) override;

    void SetMotorPtr(uintptr_t ptr) { m_motorPtr = ptr; }
    void SetLayerSystemPtr(uintptr_t ptr) { m_layerSystemPtr = ptr; }

private:
    std::shared_ptr<memory::MemoryService> m_memory;
    std::shared_ptr<native::GameActionDispatcher> m_dispatcher;

    uintptr_t m_motorPtr{0};
    uintptr_t m_layerSystemPtr{0};
    Vector3 m_currentPos;
    uint32_t m_currentMapId{1};
};

} // namespace dta::features::teleport
