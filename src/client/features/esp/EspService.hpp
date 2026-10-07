#pragma once
#include "IEspService.hpp"
#include "client/core/memory/MemoryService.hpp"
#include <memory>

namespace dta::features::esp {

class EspService : public IEspService {
public:
    explicit EspService(std::shared_ptr<memory::MemoryService> memory);

    bool WorldToScreen(const Vector3& worldPos, Vector2& outScreenPos) override;
    void UpdateCameraMatrix() override;
    std::vector<EspEntity> GetEntitiesToRender() override;

    void SetConfig(const EspConfig& config) override { m_config = config; }
    const EspConfig& GetConfig() const override { return m_config; }

    void SetCameraPtr(uintptr_t ptr) { m_cameraPtr = ptr; }
    void SetScreenSize(float width, float height) { m_screenWidth = width; m_screenHeight = height; }

private:
    std::shared_ptr<memory::MemoryService> m_memory;
    EspConfig m_config;
    Matrix4x4 m_viewMatrix{Matrix4x4::Identity()};
    uintptr_t m_cameraPtr{0};
    float m_screenWidth{1920.0f};
    float m_screenHeight{1080.0f};
};

} // namespace dta::features::esp
