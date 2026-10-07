#pragma once
#include "InsectModels.hpp"
#include "shared/Types.hpp"

namespace dta::features::insect {

class InsectPredictor {
public:
    static Vector3 PredictPosition(const Vector3& currentPos, const Vector3& velocity, float dt = 0.12f);
    static bool IsInSwingZone(const Vector3& playerPos, const Vector2& playerFacing, const Vector3& targetPos, const SwingZone& zone = {});
    static Vector3 CalculateApproachPosition(const Vector3& predictedPos, const Vector3& velocity);
};

} // namespace dta::features::insect
