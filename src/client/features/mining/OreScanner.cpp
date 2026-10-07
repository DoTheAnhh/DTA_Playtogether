#include "OreScanner.hpp"
#include <algorithm>

namespace dta::features::mining {

std::pair<std::string, bool> OreScanner::ParseOreInfo(const std::string& assetName) {
    bool isPrized = (assetName.find("gemvein") != std::string::npos) ||
                    (assetName.find("meteor") != std::string::npos) ||
                    (assetName.find("broccoli") != std::string::npos);

    std::string displayName = "Mạch đá";
    if (assetName.find("gemvein") != std::string::npos) {
        displayName = "Mạch đá quý";
    } else if (assetName.find("ore") != std::string::npos) {
        displayName = "Quặng khoáng";
    }

    if (assetName.find("_l") != std::string::npos) {
        displayName += " (Lớn)";
    } else if (assetName.find("_m") != std::string::npos) {
        displayName += " (Vừa)";
    } else if (assetName.find("_s") != std::string::npos) {
        displayName += " (Nhỏ)";
    }

    return {displayName, isPrized};
}

std::vector<OreModel> OreScanner::FilterAndSortOres(
    const std::vector<OreModel>& rawOres,
    const Vector3& playerPos,
    const MiningOptions& options) {

    std::vector<OreModel> filtered;
    filtered.reserve(rawOres.size());

    for (const auto& ore : rawOres) {
        if (ore.hp <= 0) continue;

        float dist = playerPos.Distance(ore.position);
        if (options.radius > 0.0f && dist > options.radius) continue;

        if (options.targetKind == MiningTargetKind::EVENT_ONLY && !ore.isEvent) continue;
        if (options.targetKind == MiningTargetKind::ROCK_ONLY && ore.isEvent) continue;

        filtered.push_back(ore);
    }

    // Sort: Prized first, then shortest distance
    std::sort(filtered.begin(), filtered.end(), [&](const OreModel& a, const OreModel& b) {
        if (options.prioritizePrized && a.isPrized != b.isPrized) {
            return a.isPrized > b.isPrized;
        }
        return playerPos.Distance(a.position) < playerPos.Distance(b.position);
    });

    return filtered;
}

} // namespace dta::features::mining
