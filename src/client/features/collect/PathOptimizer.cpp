#include "PathOptimizer.hpp"
#include <limits>

namespace dta::features::collect {

std::vector<CollectItemModel> PathOptimizer::OptimizeRoute(
    const Vector3& startPos,
    std::vector<CollectItemModel> items) {

    std::vector<CollectItemModel> optimized;
    optimized.reserve(items.size());

    Vector3 current = startPos;
    std::vector<bool> visited(items.size(), false);

    for (size_t step = 0; step < items.size(); ++step) {
        float bestDist = std::numeric_limits<float>::max();
        size_t bestIdx = items.size();

        for (size_t i = 0; i < items.size(); ++i) {
            if (!visited[i]) {
                float dist = current.Distance(items[i].position);
                if (dist < bestDist) {
                    bestDist = dist;
                    bestIdx = i;
                }
            }
        }

        if (bestIdx < items.size()) {
            visited[bestIdx] = true;
            optimized.push_back(items[bestIdx]);
            current = items[bestIdx].position;
        }
    }

    return optimized;
}

} // namespace dta::features::collect
