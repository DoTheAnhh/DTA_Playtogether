#include "FishingCatalog.hpp"
#include "client/core/logger/AsyncLogger.hpp"
#include <fstream>
#include <sstream>

namespace dta::features::fishing {

FishingCatalog& FishingCatalog::Instance() {
    static FishingCatalog instance;
    return instance;
}

bool FishingCatalog::LoadFromFile(const std::string& catalogPath) {
    std::ifstream file(catalogPath);
    if (!file.is_open()) {
        DTA_LOG_WARN("Fishing", "Fish catalog not found at: " + catalogPath + ". Using defaults.");
        // Seed default high-tier fish
        m_fishMap[17440] = FishModel{17440, 5, 4, "Cá Mặt Trời", false, true};
        m_fishMap[18484] = FishModel{18484, 5, 5, "Mực khổng lồ", false, false};
        m_fishMap[19592] = FishModel{19592, 4, 3, "Cá hồi đỏ đột biến", true, false};
        return true;
    }

    // Load fish list
    std::stringstream ss;
    ss << file.rdbuf();
    std::string content = ss.str();

    // Fast parser for {"fishId": X, "shadow": Y, ...}
    size_t pos = 0;
    while ((pos = content.find("\"fishId\":", pos)) != std::string::npos) {
        pos += 9;
        while (pos < content.size() && (content[pos] == ' ' || content[pos] == ':')) pos++;
        size_t endNum = content.find_first_of(",}\n", pos);
        if (endNum == std::string::npos) break;
        uint32_t fishId = static_cast<uint32_t>(std::stoul(content.substr(pos, endNum - pos)));

        uint8_t shadow = 3;
        size_t shadowPos = content.find("\"shadow\":", endNum);
        if (shadowPos != std::string::npos && shadowPos < endNum + 60) {
            shadowPos += 9;
            while (shadowPos < content.size() && content[shadowPos] == ' ') shadowPos++;
            size_t endShadow = content.find_first_of(",}\n", shadowPos);
            if (endShadow != std::string::npos) {
                shadow = static_cast<uint8_t>(std::stoul(content.substr(shadowPos, endShadow - shadowPos)));
            }
        }

        FishModel fish;
        fish.fishId = fishId;
        fish.shadowTier = shadow;
        fish.grade = (shadow >= 5) ? 5 : (shadow >= 4 ? 4 : (shadow >= 3 ? 3 : 2));
        fish.name = "Cá #" + std::to_string(fishId);
        m_fishMap[fishId] = fish;

        pos = endNum;
    }

    DTA_LOG_INFO("Fishing", "Loaded " + std::to_string(m_fishMap.size()) + " fish species into catalog.");
    return true;
}

const FishModel* FishingCatalog::FindFish(uint32_t fishId) const {
    auto it = m_fishMap.find(fishId);
    if (it != m_fishMap.end()) {
        return &it->second;
    }
    return nullptr;
}

} // namespace dta::features::fishing
