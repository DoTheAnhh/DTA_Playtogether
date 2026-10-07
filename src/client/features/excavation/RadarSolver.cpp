#include "RadarSolver.hpp"
#include <cmath>

namespace dta::features::excavation {

std::optional<Vector3> RadarSolver::TriangulateChest(
    const RadarSample& s1,
    const RadarSample& s2,
    const RadarSample& s3) {

    if (s1.signalIntensity <= 0.001f || s2.signalIntensity <= 0.001f || s3.signalIntensity <= 0.001f) {
        return std::nullopt;
    }

    // Distance is inversely proportional to square root of signal strength
    float r1 = 1.0f / std::sqrt(s1.signalIntensity);
    float r2 = 1.0f / std::sqrt(s2.signalIntensity);
    float r3 = 1.0f / std::sqrt(s3.signalIntensity);

    // Weighted barycentric combination of the 3 points
    float w1 = 1.0f / (r1 + 0.001f);
    float w2 = 1.0f / (r2 + 0.001f);
    float w3 = 1.0f / (r3 + 0.001f);
    float totalW = w1 + w2 + w3;

    if (totalW < 1e-6f) return std::nullopt;

    Vector3 estimated(
        (s1.position.x * w1 + s2.position.x * w2 + s3.position.x * w3) / totalW,
        s1.position.y,
        (s1.position.z * w1 + s2.position.z * w2 + s3.position.z * w3) / totalW
    );

    return estimated;
}

} // namespace dta::features::excavation
