#include "MemoryService.hpp"
#include <vector>

namespace dta::memory {

MemoryService::MemoryService(std::shared_ptr<device::IDeviceDriver> driver)
    : m_driver(std::move(driver)) {}

std::vector<uint8_t> MemoryService::ReadBytes(uintptr_t address, size_t size) {
    if (!m_driver || address == 0 || size == 0) return {};
    std::vector<uint8_t> buffer(size);
    if (m_driver->ReadMemoryRaw(address, buffer.data(), size)) {
        return buffer;
    }
    return {};
}

uint64_t MemoryService::ReadU64(uintptr_t address) {
    return Read<uint64_t>(address);
}

uint32_t MemoryService::ReadU32(uintptr_t address) {
    return Read<uint32_t>(address);
}

int32_t MemoryService::ReadI32(uintptr_t address) {
    return Read<int32_t>(address);
}

float MemoryService::ReadFloat(uintptr_t address) {
    return Read<float>(address);
}

bool MemoryService::IsValidPointer(uintptr_t ptr) const noexcept {
    if (ptr == 0) return false;
    // Basic alignment and user-space bounds check
    if ((ptr & (sizeof(void*) - 1)) != 0) return false;
    if (ptr < 0x10000 || ptr > 0x7FFFFFFFFFFF) return false;
    return true;
}

bool MemoryService::IsUnityObjectAlive(uintptr_t unityObjPtr) {
    if (!IsValidPointer(unityObjPtr)) return false;
    // In UnityEngine.Object, m_CachedPtr is at offset 0x10 (16)
    uintptr_t cachedPtr = ReadU64(unityObjPtr + 0x10);
    return cachedPtr != 0;
}

std::string MemoryService::ReadIl2CppString(uintptr_t strPtr) {
    if (!IsValidPointer(strPtr)) return "";

    // IL2CPP System.String layout:
    // +0x10: int32_t length (number of characters)
    // +0x14: char16_t chars[length]
    int32_t length = ReadI32(strPtr + 0x10);
    if (length <= 0 || length > 2048) return "";

    std::vector<char16_t> chars(length);
    if (!m_driver->ReadMemoryRaw(strPtr + 0x14, chars.data(), length * sizeof(char16_t))) {
        return "";
    }

    std::string result;
    result.reserve(length);
    for (char16_t c : chars) {
        if (c < 128) {
            result.push_back(static_cast<char>(c));
        } else {
            result.push_back('?'); // Simplified UTF-16 to ASCII
        }
    }
    return result;
}

} // namespace dta::memory
