#pragma once
#include "IDeviceDriver.hpp"
#include <windows.h>
#include <unordered_map>

namespace dta::device {

class MEmuDriver : public IDeviceDriver {
public:
    MEmuDriver();
    ~MEmuDriver() override;

    bool DetectAndAttach() override;
    void Detach() override;
    bool IsProcessAlive() const override;
    uintptr_t GetModuleBase(std::string_view moduleName) override;
    bool ReadMemoryRaw(uintptr_t address, void* buffer, size_t size) override;
    bool WriteMemoryRaw(uintptr_t address, const void* buffer, size_t size) override;
    EmulatorType GetType() const override { return EmulatorType::MEMU; }
    std::string GetDeviceName() const override { return "MEmu v9+ (MEmuConsole)"; }
    uint32_t GetProcessId() const override { return m_pid; }

private:
    HANDLE m_hProcess{nullptr};
    DWORD m_pid{0};
    std::unordered_map<std::string, uintptr_t> m_moduleCache;

    bool FindProcessByName(const wchar_t* processName);
    void RefreshModules();
};

} // namespace dta::device
