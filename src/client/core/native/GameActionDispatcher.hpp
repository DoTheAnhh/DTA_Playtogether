#pragma once
#include <cstdint>
#include <string>
#include <memory>
#include "shared/Types.hpp"
#include "client/core/memory/MemoryService.hpp"
#include "NativeDispatcher.hpp"

namespace dta::native {

class GameActionDispatcher {
public:
    static GameActionDispatcher& Instance();

    void Init(std::shared_ptr<memory::MemoryService> memory);

    // Fishing Actions
    bool ClickFishing(uintptr_t toolPtr);
    bool KeepFish(uintptr_t dialogPtr);
    bool SellFish(uintptr_t dialogPtr);
    bool OpenFishBox(uintptr_t dialogPtr);

    // Mining Actions
    bool ClickPickax(uintptr_t toolPtr);

    // Shovel Actions
    bool ClickShovel(uintptr_t toolPtr);

    // Insect Actions
    bool ClickInsect(uintptr_t toolPtr);

    // Common Tool Attach Action
    bool ClickAttachAction(uintptr_t toolPtr, int32_t index = 0);

    // Movement & Map
    bool SetTransientPosition(uintptr_t motorPtr, const Vector3& pos);
    bool ConnectToZoneMove(uintptr_t layerSystemPtr, uint32_t targetMapId, int32_t fromType = 10);

    // Field Item Pickup
    bool PickFieldObject(uintptr_t actorPtr, uint32_t uid);

    // Dialog & UI Controls
    bool CloseResultItem(uintptr_t dialogPtr);
    bool SkipResultItem(uintptr_t dialogPtr);
    bool RepairItem(uintptr_t dialogPtr);
    bool CloseRepair(uintptr_t dialogPtr);
    bool ConfirmOk(uintptr_t dialogPtr);
    bool ConfirmQuestionOk(uintptr_t dialogPtr);

    // Jump & Joystick
    bool PressJump(uintptr_t joystickPtr);
    bool ReleaseJump(uintptr_t joystickPtr);

private:
    GameActionDispatcher() = default;
    std::shared_ptr<memory::MemoryService> m_memory;

    bool ValidateCall(uintptr_t instancePtr, std::string_view actionName);
};

} // namespace dta::native
