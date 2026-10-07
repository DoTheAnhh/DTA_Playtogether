#pragma once
#include "IDeviceDriver.hpp"
#include <memory>
#include <vector>

namespace dta::device {

class DeviceManager {
public:
    static DeviceManager& Instance();

    bool AutoDetect();
    void SetDriver(std::shared_ptr<IDeviceDriver> driver);
    [[nodiscard]] std::shared_ptr<IDeviceDriver> GetCurrentDriver() const;
    [[nodiscard]] bool IsConnected() const;

private:
    DeviceManager();
    std::shared_ptr<IDeviceDriver> m_activeDriver;
    std::vector<std::shared_ptr<IDeviceDriver>> m_supportedDrivers;
};

} // namespace dta::device
