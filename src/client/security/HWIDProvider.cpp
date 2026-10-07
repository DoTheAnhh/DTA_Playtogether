#include "HWIDProvider.hpp"
#include <windows.h>
#include <intrin.h>
#include <sstream>
#include <iomanip>

namespace dta::security {

std::string HWIDProvider::GetHWID() {
    int cpuInfo[4] = { 0 };
    __cpuid(cpuInfo, 0);
    uint32_t cpuPart1 = static_cast<uint32_t>(cpuInfo[1]);
    uint32_t cpuPart2 = static_cast<uint32_t>(cpuInfo[3]);
    __cpuid(cpuInfo, 1);
    uint32_t cpuPart3 = static_cast<uint32_t>(cpuInfo[0]);

    DWORD volumeSerial = 0;
    GetVolumeInformationA("C:\\", nullptr, 0, &volumeSerial, nullptr, nullptr, nullptr, 0);

    char computerName[MAX_COMPUTERNAME_LENGTH + 1] = { 0 };
    DWORD size = sizeof(computerName);
    GetComputerNameA(computerName, &size);

    uint32_t nameHash = 0x811c9dc5;
    for (DWORD i = 0; i < size; ++i) {
        nameHash ^= static_cast<uint8_t>(computerName[i]);
        nameHash *= 0x01000193;
    }

    std::stringstream ss;
    ss << std::hex << std::uppercase << std::setfill('0')
       << std::setw(8) << cpuPart1 << "-"
       << std::setw(8) << cpuPart2 << "-"
       << std::setw(8) << (cpuPart3 ^ volumeSerial) << "-"
       << std::setw(8) << nameHash;
    return ss.str();
}

} // namespace dta::security
