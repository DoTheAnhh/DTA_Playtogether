#pragma once
#include <vector>
#include "EspModels.hpp"
#include "Math3D.hpp"

namespace dta::features::esp {

class IEspService {
public:
    virtual ~IEspService() = default;

    virtual bool WorldToScreen(const Vector3& worldPos, Vector2& outScreenPos) = 0;
    virtual void UpdateCameraMatrix() = 0;
    virtual std::vector<EspEntity> GetEntitiesToRender() = 0;

    virtual void SetConfig(const EspConfig& config) = 0;
    virtual const EspConfig& GetConfig() const = 0;
};

} // namespace dta::features::esp
