#pragma once
#include <vector>
#include <cstdint>
#include <optional>
#include "InsectModels.hpp"
#include "shared/Types.hpp"

namespace dta::features::insect {

class IInsectService {
public:
    virtual ~IInsectService() = default;

    virtual bool SwingNet() = 0;
    virtual bool ApproachInsect(const Vector3& targetPos) = 0;
    virtual bool FreezeInsect(uint32_t insectUid) = 0;
    virtual bool RepairNet() = 0;
    virtual bool CloseResultDialog() = 0;

    virtual std::vector<InsectModel> ScanInsectsAround(const Vector3& playerPos, float radius) = 0;
    virtual bool IsNetBroken() = 0;
    virtual bool IsResultDialogOpen() = 0;
};

} // namespace dta::features::insect
