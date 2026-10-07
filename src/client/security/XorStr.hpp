#pragma once
#include <string>
#include <array>
#include <cstddef>
#include <cstdint>

namespace dta::security {

template <size_t N, uint32_t Seed = 0x5A7D3C>
class XorString {
private:
    std::array<char, N> m_data;

public:
    constexpr explicit XorString(const char(&str)[N]) noexcept {
        for (size_t i = 0; i < N; ++i) {
            m_data[i] = static_cast<char>(str[i] ^ static_cast<char>((Seed + i) % 255));
        }
    }

    [[nodiscard]] std::string Decrypt() const {
        std::string res;
        if (N <= 1) return res;
        res.resize(N - 1);
        for (size_t i = 0; i < N - 1; ++i) {
            res[i] = static_cast<char>(m_data[i] ^ static_cast<char>((Seed + i) % 255));
        }
        return res;
    }
};

} // namespace dta::security

#define DTA_XOR(str) (::dta::security::XorString<sizeof(str)>(str).Decrypt())
