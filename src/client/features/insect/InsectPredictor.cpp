#include "InsectPredictor.hpp"
#include <cmath>

namespace dta::features::insect {

Vector3 InsectPredictor::PredictPosition(const Vector3& currentPos, const Vector3& velocity, float dt) {
    return Vector3(
        currentPos.x + velocity.x * dt,
        currentPos.y + velocity.y * dt,
        currentPos.z + velocity.z * dt
    );
}

bool InsectPredictor::IsInSwingZone(const Vector3& playerPos, const Vector2& playerFacing, const Vector3& targetPos, const SwingZone& zone) {
    float relY = targetPos.y - playerPos.y;
    if (relY < zone.low || relY > zone.high) return false;

    float dx = targetPos.x - playerPos.x;
    float dz = targetPos.z - playerPos.z;

    float forwardDist = dx * playerFacing.x + dz * playerFacing.y;
    float crossDist = std::abs(dx * (-playerFacing.y) + dz * playerFacing.x);

    if (forwardDist < 0.1f || forwardDist > zone.reach) return false;
    if (crossDist > zone.radius) return false;

    return true;
}

Vector3 InsectPredictor::CalculateApproachPosition(const Vector3& predictedPos, const Vector3& velocity) {
    Vector3 dir = velocity.Normalized();
    // Approach 1.8m behind or opposite to flight path
    return Vector3(
        predictedPos.x - dir.x * 1.8f,
        predictedPos.y,
        predictedPos.z - dir.z * 1.8f
    );
}

} // namespace dta::features::insect
