#pragma once
#include <string>
#include <unordered_map>
#include <vector>
#include "shared/FeatureModels.hpp"

namespace dta::features::fishing {

class FishingCatalog {
public:
    static FishingCatalog& Instance();

    bool LoadFromFile(const std::string& catalogPath);
    [[nodiscard]] const FishModel* FindFish(uint32_t fishId) const;
    [[nodiscard]] size_t GetFishCount() const { return m_fishMap.size(); }

private:
    FishingCatalog() = default;
    std::unordered_map<uint32_t, FishModel> m_fishMap;
};

} // namespace dta::features::fishing
