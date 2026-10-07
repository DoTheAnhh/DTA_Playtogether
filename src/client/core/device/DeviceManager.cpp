#include "DeviceManager.hpp"
#include "LDPlayer9Driver.hpp"
#include "MEmuDriver.hpp"
#include "client/core/logger/AsyncLogger.hpp"

namespace dta::device {

DeviceManager::DeviceManager() {
    m_supportedDrivers.push_back(std::make_shared<LDPlayer9Driver>());
    m_supportedDrivers.push_back(std::make_shared<MEmuDriver>());
}

DeviceManager& DeviceManager::Instance() {
    static DeviceManager instance;
    return instance;
}

bool DeviceManager::AutoDetect() {
    DTA_LOG_INFO("Device", "Scanning for active emulators (LDPlayer 9+, MEmu)...");
    for (const auto& driver : m_supportedDrivers) {
        if (driver->DetectAndAttach()) {
            m_activeDriver = driver;
            DTA_LOG_INFO("Device", "Active device set to: " + driver->GetDeviceName());
            return true;
        }
    }
    DTA_LOG_WARN("Device", "No supported emulator detected. Waiting for game process...");
    return false;
}

void DeviceManager::SetDriver(std::shared_ptr<IDeviceDriver> driver) {
    m_activeDriver = driver;
}

std::shared_ptr<IDeviceDriver> DeviceManager::GetCurrentDriver() const {
    return m_activeDriver;
}

bool DeviceManager::IsConnected() const {
    return m_activeDriver && m_activeDriver->IsProcessAlive();
}

} // namespace dta::device
