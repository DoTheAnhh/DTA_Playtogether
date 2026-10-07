#pragma once
#include "shared/Types.hpp"

namespace dta::features::esp {

inline bool WorldToScreen(
    const Vector3& world,
    Vector2& screen,
    const Matrix4x4& viewMatrix,
    float screenWidth,
    float screenHeight) {

    float w = world.x * viewMatrix.m[0][3] + world.y * viewMatrix.m[1][3] + world.z * viewMatrix.m[2][3] + viewMatrix.m[3][3];
    if (w < 0.01f) return false; // Behind camera

    float x = world.x * viewMatrix.m[0][0] + world.y * viewMatrix.m[1][0] + world.z * viewMatrix.m[2][0] + viewMatrix.m[3][0];
    float y = world.x * viewMatrix.m[0][1] + world.y * viewMatrix.m[1][1] + world.z * viewMatrix.m[2][1] + viewMatrix.m[3][1];

    float invW = 1.0f / w;
    float ndcX = x * invW;
    float ndcY = y * invW;

    screen.x = (screenWidth * 0.5f) + (ndcX * screenWidth * 0.5f);
    screen.y = (screenHeight * 0.5f) - (ndcY * screenHeight * 0.5f);
    return true;
}

} // namespace dta::features::esp
