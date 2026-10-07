#pragma once
#include <vector>
#include <string>
#include "MiningModels.hpp"
#include "shared/Types.hpp"

namespace dta::features::mining {

class OreScanner {
public:
    static std::vector<OreModel> FilterAndSortOres(
        const std::vector<OreModel>& rawOres,
        const Vector3& playerPos,
        const MiningOptions& options);

    static std::pair<std::string, bool> ParseOreInfo(const std::string& assetName);
};

} // namespace dta::features::mining
