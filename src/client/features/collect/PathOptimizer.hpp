#pragma once
#include <vector>
#include "CollectModels.hpp"
#include "shared/Types.hpp"

namespace dta::features::collect {

class PathOptimizer {
public:
    static std::vector<CollectItemModel> OptimizeRoute(
        const Vector3& startPos,
        std::vector<CollectItemModel> items);
};

} // namespace dta::features::collect
