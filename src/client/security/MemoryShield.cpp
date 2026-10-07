#include "MemoryShield.hpp"
#include <windows.h>
#include <cstring>

namespace dta::security {

uint32_t MemoryShield::CalculateCRC32(std::span<const uint8_t> data) {
    uint32_t crc = 0xFFFFFFFF;
    for (uint8_t byte : data) {
        crc ^= byte;
        for (int j = 0; j < 8; ++j) {
            crc = (crc >> 1) ^ (0xEDB88320 & (-(crc & 1)));
        }
    }
    return ~crc;
}

bool MemoryShield::VerifySectionIntegrity(void* moduleBase, const char* sectionName, uint32_t expectedCRC) {
    if (!moduleBase || !sectionName) return false;

    auto* dosHeader = reinterpret_cast<PIMAGE_DOS_HEADER>(moduleBase);
    if (dosHeader->e_magic != IMAGE_DOS_SIGNATURE) return false;

    auto* ntHeaders = reinterpret_cast<PIMAGE_NT_HEADERS>(
        reinterpret_cast<uint8_t*>(moduleBase) + dosHeader->e_lfanew);
    if (ntHeaders->Signature != IMAGE_NT_SIGNATURE) return false;

    auto* sectionHeader = IMAGE_FIRST_SECTION(ntHeaders);
    for (WORD i = 0; i < ntHeaders->FileHeader.NumberOfSections; ++i, ++sectionHeader) {
        char currentName[9] = { 0 };
        std::memcpy(currentName, sectionHeader->Name, 8);
        if (std::strcmp(currentName, sectionName) == 0) {
            auto* sectionData = reinterpret_cast<const uint8_t*>(moduleBase) + sectionHeader->VirtualAddress;
            size_t sectionSize = sectionHeader->Misc.VirtualSize;
            uint32_t currentCRC = CalculateCRC32(std::span<const uint8_t>(sectionData, sectionSize));
            return currentCRC == expectedCRC;
        }
    }

    return false;
}

} // namespace dta::security
