#include "EspService.hpp"

namespace dta::features::esp {

EspService::EspService(std::shared_ptr<memory::MemoryService> memory)
    : m_memory(std::move(memory)) {}

bool EspService::WorldToScreen(const Vector3& worldPos, Vector2& outScreenPos) {
    return ::dta::features::esp::WorldToScreen(
        worldPos,
        outScreenPos,
        m_viewMatrix,
        m_screenWidth,
        m_screenHeight
    );
}

void EspService::UpdateCameraMatrix() {
    if (!m_memory || m_cameraPtr == 0) return;
    // Reads 64-byte 4x4 matrix from Camera.main worldToCameraMatrix / cullingMatrix
    m_viewMatrix = m_memory->Read<Matrix4x4>(m_cameraPtr);
}

std::vector<EspEntity> EspService::GetEntitiesToRender() {
    if (!m_config.enabled) return {};
    std::vector<EspEntity> list;
    return list;
}

} // namespace dta::features::esp
