#pragma once
#include <string>
#include <vector>
#include <cstdint>
#include "TeleportModels.hpp"
#include "shared/Types.hpp"
#include "shared/FeatureModels.hpp"

namespace dta::features::teleport {

class ITeleportService {
public:
    virtual ~ITeleportService() = default;

    virtual bool TeleportTo(const Vector3& targetPos) = 0;
    virtual Vector3 GetCurrentPosition() = 0;

    virtual bool SwitchZone(uint32_t targetMapId, int32_t fromType = 10) = 0;
    virtual uint32_t GetCurrentMapId() = 0;

    virtual void SaveWaypoint(const std::string& name, const Vector3& pos, uint32_t mapId) = 0;
    virtual std::vector<Waypoint> GetWaypoints(uint32_t mapId) = 0;
    virtual bool DeleteWaypoint(const std::string& name) = 0;
};

} // namespace dta::features::teleport
