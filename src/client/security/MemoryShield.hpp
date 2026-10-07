#pragma once
#include <cstdint>
#include <cstddef>
#include <span>

namespace dta::security {

class MemoryShield {
public:
    static uint32_t CalculateCRC32(std::span<const uint8_t> data);
    static bool VerifySectionIntegrity(void* moduleBase, const char* sectionName, uint32_t expectedCRC);
};

} // namespace dta::security
