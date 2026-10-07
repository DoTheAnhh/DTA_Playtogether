#include "MasterTeleportService.hpp"
#include <sstream>

namespace dta::server::services {

MasterTeleportService::MasterTeleportService() {
    InitDefaults();
}

MasterTeleportService& MasterTeleportService::Instance() {
    static MasterTeleportService instance;
    return instance;
}

void MasterTeleportService::InitDefaults() {
    std::lock_guard<std::mutex> lock(m_mutex);
    m_spots.clear();

    // Default Master Teleport Locations for all clients
    m_spots.push_back(MasterTeleportSpot{
        "spot_plaza_lake", "Plaza - Hồ Cá Lớn", 1, "Plaza", Vector3(-12.4f, 0.5f, 45.2f), true
    });
    m_spots.push_back(MasterTeleportSpot{
        "spot_plaza_dock", "Plaza - Bến Cảng Biển", 1, "Plaza", Vector3(34.8f, 1.2f, -88.5f), true
    });
    m_spots.push_back(MasterTeleportSpot{
        "spot_downtown_mine", "Downtown - Mỏ Kim Cương", 2, "Downtown", Vector3(112.5f, -5.0f, 64.3f), true
    });
    m_spots.push_back(MasterTeleportSpot{
        "spot_camp_stream", "Camping - Suối Bắt Bọ Hiếm", 3, "Camping", Vector3(-45.0f, 12.0f, 130.5f), true
    });
    m_spots.push_back(MasterTeleportSpot{
        "spot_resort_ocean", "Resort - Biển Cá Heo / Voi", 4, "Resort", Vector3(15.2f, 0.2f, 210.0f), true
    });
    m_spots.push_back(MasterTeleportSpot{
        "spot_home_farm", "Home - Sân Vườn Nông Trại", 10, "Home", Vector3(0.0f, 0.0f, 5.0f), true
    });
}

std::vector<MasterTeleportSpot> MasterTeleportService::GetAllSpots() {
    std::lock_guard<std::mutex> lock(m_mutex);
    return m_spots;
}

std::string MasterTeleportService::GetSpotsJson() {
    std::lock_guard<std::mutex> lock(m_mutex);
    std::stringstream ss;
    ss << "[\n";
    for (size_t i = 0; i < m_spots.size(); ++i) {
        const auto& s = m_spots[i];
        ss << "  {\n"
           << "    \"id\": \"" << s.id << "\",\n"
           << "    \"name\": \"" << s.name << "\",\n"
           << "    \"mapId\": " << s.mapId << ",\n"
           << "    \"mapName\": \"" << s.mapName << "\",\n"
           << "    \"x\": " << s.position.x << ",\n"
           << "    \"y\": " << s.position.y << ",\n"
           << "    \"z\": " << s.position.z << ",\n"
           << "    \"enabled\": " << (s.enabled ? "true" : "false") << "\n"
           << "  }" << (i + 1 < m_spots.size() ? ",\n" : "\n");
    }
    ss << "]";
    return ss.str();
}

void MasterTeleportService::AddOrUpdateSpot(const MasterTeleportSpot& spot) {
    std::lock_guard<std::mutex> lock(m_mutex);
    for (auto& s : m_spots) {
        if (s.id == spot.id || s.name == spot.name) {
            s = spot;
            return;
        }
    }
    m_spots.push_back(spot);
}

bool MasterTeleportService::DeleteSpot(const std::string& id) {
    std::lock_guard<std::mutex> lock(m_mutex);
    for (auto it = m_spots.begin(); it != m_spots.end(); ++it) {
        if (it->id == id) {
            m_spots.erase(it);
            return true;
        }
    }
    return false;
}

bool MasterTeleportService::ToggleSpot(const std::string& id) {
    std::lock_guard<std::mutex> lock(m_mutex);
    for (auto& s : m_spots) {
        if (s.id == id) {
            s.enabled = !s.enabled;
            return true;
        }
    }
    return false;
}

} // namespace dta::server::services
