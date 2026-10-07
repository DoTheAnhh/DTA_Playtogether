#pragma once
#include <string>
#include <string_view>
#include <cstdint>
#include <span>
#include <memory>
#include "shared/GameEnums.hpp"

namespace dta::device {

class IDeviceDriver {
public:
    virtual ~IDeviceDriver() = default;

    virtual bool DetectAndAttach() = 0;
    virtual void Detach() = 0;
    virtual bool IsProcessAlive() const = 0;
    virtual uintptr_t GetModuleBase(std::string_view moduleName) = 0;
    virtual bool ReadMemoryRaw(uintptr_t address, void* buffer, size_t size) = 0;
    virtual bool WriteMemoryRaw(uintptr_t address, const void* buffer, size_t size) = 0;
    virtual EmulatorType GetType() const = 0;
    virtual std::string GetDeviceName() const = 0;
    virtual uint32_t GetProcessId() const = 0;
};

} // namespace dta::device
