#pragma once
#include <memory>
#include <string>
#include <string_view>
#include <span>
#include <concepts>
#include <type_traits>
#include <vector>
#include "client/core/device/IDeviceDriver.hpp"

namespace dta::memory {

template <typename T>
concept TriviallyCopyable = std::is_trivially_copyable_v<T>;

class MemoryService {
public:
    explicit MemoryService(std::shared_ptr<device::IDeviceDriver> driver);

    void SetDriver(std::shared_ptr<device::IDeviceDriver> driver) {
        m_driver = driver;
    }

    [[nodiscard]] std::shared_ptr<device::IDeviceDriver> GetDriver() const {
        return m_driver;
    }

    template <TriviallyCopyable T>
    T Read(uintptr_t address) {
        T value{};
        if (m_driver && address != 0) {
            m_driver->ReadMemoryRaw(address, &value, sizeof(T));
        }
        return value;
    }

    template <TriviallyCopyable T>
    bool Write(uintptr_t address, const T& value) {
        if (!m_driver || address == 0) return false;
        return m_driver->WriteMemoryRaw(address, &value, sizeof(T));
    }

    template <TriviallyCopyable T>
    bool ReadBatch(uintptr_t baseAddress, std::span<T> outSpan) {
        if (!m_driver || baseAddress == 0) return false;
        return m_driver->ReadMemoryRaw(baseAddress, outSpan.data(), outSpan.size_bytes());
    }

    [[nodiscard]] std::vector<uint8_t> ReadBytes(uintptr_t address, size_t size);
    [[nodiscard]] uint64_t ReadU64(uintptr_t address);
    [[nodiscard]] uint32_t ReadU32(uintptr_t address);
    [[nodiscard]] int32_t  ReadI32(uintptr_t address);
    [[nodiscard]] float    ReadFloat(uintptr_t address);

    [[nodiscard]] std::string ReadIl2CppString(uintptr_t strPtr);
    [[nodiscard]] bool IsValidPointer(uintptr_t ptr) const noexcept;

    // Unity Object active verification (m_CachedPtr != 0)
    [[nodiscard]] bool IsUnityObjectAlive(uintptr_t unityObjPtr);

private:
    std::shared_ptr<device::IDeviceDriver> m_driver;
};

} // namespace dta::memory
