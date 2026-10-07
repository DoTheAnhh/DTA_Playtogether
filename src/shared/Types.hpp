#pragma once
#include <cstdint>
#include <cmath>
#include <string>
#include <string_view>
#include <vector>
#include <array>

namespace dta {

struct Vector2 {
    float x{0.0f};
    float y{0.0f};

    constexpr Vector2() = default;
    constexpr Vector2(float x_, float y_) : x(x_), y(y_) {}

    [[nodiscard]] float Length() const noexcept {
        return std::sqrt(x * x + y * y);
    }

    [[nodiscard]] float Distance(const Vector2& o) const noexcept {
        float dx = x - o.x;
        float dy = y - o.y;
        return std::sqrt(dx * dx + dy * dy);
    }
};

struct Vector3 {
    float x{0.0f};
    float y{0.0f};
    float z{0.0f};

    constexpr Vector3() = default;
    constexpr Vector3(float x_, float y_, float z_) : x(x_), y(y_), z(z_) {}

    [[nodiscard]] float Length() const noexcept {
        return std::sqrt(x * x + y * y + z * z);
    }

    [[nodiscard]] float LengthXZ() const noexcept {
        return std::sqrt(x * x + z * z);
    }

    [[nodiscard]] float Distance(const Vector3& o) const noexcept {
        float dx = x - o.x;
        float dy = y - o.y;
        float dz = z - o.z;
        return std::sqrt(dx * dx + dy * dy + dz * dz);
    }

    [[nodiscard]] float DistanceXZ(const Vector3& o) const noexcept {
        float dx = x - o.x;
        float dz = z - o.z;
        return std::sqrt(dx * dx + dz * dz);
    }

    [[nodiscard]] Vector3 Normalized() const noexcept {
        float len = Length();
        if (len < 1e-6f) return Vector3(0.0f, 0.0f, 0.0f);
        float inv = 1.0f / len;
        return Vector3(x * inv, y * inv, z * inv);
    }

    constexpr Vector3 operator+(const Vector3& o) const noexcept {
        return Vector3(x + o.x, y + o.y, z + o.z);
    }

    constexpr Vector3 operator-(const Vector3& o) const noexcept {
        return Vector3(x - o.x, y - o.y, z - o.z);
    }

    constexpr Vector3 operator*(float s) const noexcept {
        return Vector3(x * s, y * s, z * s);
    }
};

struct Matrix4x4 {
    float m[4][4]{};

    constexpr Matrix4x4() = default;

    static Matrix4x4 Identity() noexcept {
        Matrix4x4 res;
        for (int i = 0; i < 4; ++i) {
            res.m[i][i] = 1.0f;
        }
        return res;
    }
};

struct ColorRGBA {
    uint8_t r{255};
    uint8_t g{255};
    uint8_t b{255};
    uint8_t a{255};

    constexpr ColorRGBA() = default;
    constexpr ColorRGBA(uint8_t r_, uint8_t g_, uint8_t b_, uint8_t a_ = 255)
        : r(r_), g(g_), b(b_), a(a_) {}

    [[nodiscard]] uint32_t ToU32() const noexcept {
        return (static_cast<uint32_t>(a) << 24) |
               (static_cast<uint32_t>(b) << 16) |
               (static_cast<uint32_t>(g) << 8)  |
               (static_cast<uint32_t>(r));
    }
};

} // namespace dta
