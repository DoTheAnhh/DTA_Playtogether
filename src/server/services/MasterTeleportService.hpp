#pragma once
#include <string>
#include <vector>
#include <mutex>
#include <cstdint>
#include "shared/Types.hpp"

namespace dta::server::services {

struct MasterTeleportSpot {
    std::string id;
    std::string name;
    uint32_t mapId;
    std::string mapName;
    Vector3 position;
    bool enabled{true};
};

class MasterTeleportService {
public:
    static MasterTeleportService& Instance();

    void InitDefaults();
    [[nodiscard]] std::vector<MasterTeleportSpot> GetAllSpots();
    [[nodiscard]] std::string GetSpotsJson();

    void AddOrUpdateSpot(const MasterTeleportSpot& spot);
    bool DeleteSpot(const std::string& id);
    bool ToggleSpot(const std::string& id);

private:
    MasterTeleportService();
    std::mutex m_mutex;
    std::vector<MasterTeleportSpot> m_spots;
};

} // namespace dta::server::services
