#pragma once
#include <optional>
#include "ExcavationModels.hpp"
#include "shared/Types.hpp"

namespace dta::features::excavation {

class RadarSolver {
public:
    static std::optional<Vector3> TriangulateChest(
        const RadarSample& s1,
        const RadarSample& s2,
        const RadarSample& s3);
};

} // namespace dta::features::excavation
