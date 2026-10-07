#include "GameActionDispatcher.hpp"
#include "client/core/memory/OffsetProvider.hpp"
#include "client/core/logger/AsyncLogger.hpp"

namespace dta::native {

GameActionDispatcher& GameActionDispatcher::Instance() {
    static GameActionDispatcher instance;
    return instance;
}

void GameActionDispatcher::Init(std::shared_ptr<memory::MemoryService> memory) {
    m_memory = std::move(memory);
    DTA_LOG_INFO("Dispatcher", "GameActionDispatcher initialized with MemoryService.");
}

bool GameActionDispatcher::ValidateCall(uintptr_t instancePtr, std::string_view actionName) {
    if (!m_memory) {
        DTA_LOG_ERROR("Dispatcher", "MemoryService not initialized for " + std::string(actionName));
        return false;
    }
    if (!m_memory->IsValidPointer(instancePtr)) {
        DTA_LOG_WARN("Dispatcher", "Invalid instance pointer for " + std::string(actionName) + ": 0x" + std::to_string(instancePtr));
        return false;
    }
    return true;
}

bool GameActionDispatcher::ClickFishing(uintptr_t toolPtr) {
    if (!ValidateCall(toolPtr, "ClickFishing")) return false;
    DTA_LOG_INFO("Fishing", "Native Call -> FishingPoleController.OnClick_Button(0)");
    return true;
}

bool GameActionDispatcher::KeepFish(uintptr_t dialogPtr) {
    if (!ValidateCall(dialogPtr, "KeepFish")) return false;
    DTA_LOG_INFO("Fishing", "Native Call -> DialogFishingGetItem.OnClick_ButtonClose");
    return true;
}

bool GameActionDispatcher::SellFish(uintptr_t dialogPtr) {
    if (!ValidateCall(dialogPtr, "SellFish")) return false;
    DTA_LOG_INFO("Fishing", "Native Call -> DialogFishingGetItem.OnClick_Selling");
    return true;
}

bool GameActionDispatcher::OpenFishBox(uintptr_t dialogPtr) {
    if (!ValidateCall(dialogPtr, "OpenFishBox")) return false;
    DTA_LOG_INFO("Fishing", "Native Call -> DialogFishingGetItem.OnClick_OpenPackagePopup");
    return true;
}

bool GameActionDispatcher::ClickPickax(uintptr_t toolPtr) {
    if (!ValidateCall(toolPtr, "ClickPickax")) return false;
    DTA_LOG_INFO("Mining", "Native Call -> PickaxController.OnClick_Button(0)");
    return true;
}

bool GameActionDispatcher::ClickShovel(uintptr_t toolPtr) {
    if (!ValidateCall(toolPtr, "ClickShovel")) return false;
    DTA_LOG_INFO("Excavation", "Native Call -> ShovelController.OnClick_Button(0)");
    return true;
}

bool GameActionDispatcher::ClickInsect(uintptr_t toolPtr) {
    if (!ValidateCall(toolPtr, "ClickInsect")) return false;
    DTA_LOG_INFO("Insect", "Native Call -> InsectNetController.OnClick_Button(0)");
    return true;
}

bool GameActionDispatcher::ClickAttachAction(uintptr_t toolPtr, int32_t index) {
    if (!ValidateCall(toolPtr, "ClickAttachAction")) return false;
    DTA_LOG_INFO("Dispatcher", "Native Call -> AttachActionButton.OnClickActionButton(" + std::to_string(index) + ")");
    return true;
}

bool GameActionDispatcher::SetTransientPosition(uintptr_t motorPtr, const Vector3& pos) {
    if (!ValidateCall(motorPtr, "SetTransientPosition")) return false;
    if (m_memory) {
        // Direct write to KinematicCharacterMotor.TransientPosition (offset 0x100)
        uintptr_t posOffset = motorPtr + memory::OffsetProvider::Instance().GetField().motorTransientPosition;
        m_memory->Write<float>(posOffset + 0, pos.x);
        m_memory->Write<float>(posOffset + 4, pos.y);
        m_memory->Write<float>(posOffset + 8, pos.z);
    }
    DTA_LOG_DEBUG("Teleport", "SetTransientPosition -> (" + std::to_string(pos.x) + ", " + std::to_string(pos.y) + ", " + std::to_string(pos.z) + ")");
    return true;
}

bool GameActionDispatcher::ConnectToZoneMove(uintptr_t layerSystemPtr, uint32_t targetMapId, int32_t fromType) {
    if (!ValidateCall(layerSystemPtr, "ConnectToZoneMove")) return false;
    DTA_LOG_INFO("Teleport", "Native Call -> LayerSystem.ConnectToZoneMove(mapId=" + std::to_string(targetMapId) + ", from=" + std::to_string(fromType) + ")");
    return true;
}

bool GameActionDispatcher::PickFieldObject(uintptr_t actorPtr, uint32_t uid) {
    if (!ValidateCall(actorPtr, "PickFieldObject")) return false;
    DTA_LOG_INFO("Collect", "Native Call -> OnPickFieldObject(uid=" + std::to_string(uid) + ")");
    return true;
}

bool GameActionDispatcher::CloseResultItem(uintptr_t dialogPtr) {
    if (!ValidateCall(dialogPtr, "CloseResultItem")) return false;
    DTA_LOG_INFO("Dispatcher", "Native Call -> DialogResultGetItemView.OnClick_ButtonClose");
    return true;
}

bool GameActionDispatcher::SkipResultItem(uintptr_t dialogPtr) {
    if (!ValidateCall(dialogPtr, "SkipResultItem")) return false;
    DTA_LOG_INFO("Dispatcher", "Native Call -> DialogResultGetItemView.OnClick_ButtonSkip");
    return true;
}

bool GameActionDispatcher::RepairItem(uintptr_t dialogPtr) {
    if (!ValidateCall(dialogPtr, "RepairItem")) return false;
    DTA_LOG_INFO("Dispatcher", "Native Call -> DialogItemRepair.OnClick_Repair");
    return true;
}

bool GameActionDispatcher::CloseRepair(uintptr_t dialogPtr) {
    if (!ValidateCall(dialogPtr, "CloseRepair")) return false;
    DTA_LOG_INFO("Dispatcher", "Native Call -> DialogItemRepair.OnClick_Close");
    return true;
}

bool GameActionDispatcher::ConfirmOk(uintptr_t dialogPtr) {
    if (!ValidateCall(dialogPtr, "ConfirmOk")) return false;
    DTA_LOG_INFO("Dispatcher", "Native Call -> DialogBoxMessage.OnClick_OK");
    return true;
}

bool GameActionDispatcher::ConfirmQuestionOk(uintptr_t dialogPtr) {
    if (!ValidateCall(dialogPtr, "ConfirmQuestionOk")) return false;
    DTA_LOG_INFO("Dispatcher", "Native Call -> DialogBoxQuestion.OnClick_OK");
    return true;
}

bool GameActionDispatcher::PressJump(uintptr_t joystickPtr) {
    if (!ValidateCall(joystickPtr, "PressJump")) return false;
    DTA_LOG_INFO("Dispatcher", "Native Call -> DialogJoyStick.OnPress_JumpButton");
    return true;
}

bool GameActionDispatcher::ReleaseJump(uintptr_t joystickPtr) {
    if (!ValidateCall(joystickPtr, "ReleaseJump")) return false;
    DTA_LOG_INFO("Dispatcher", "Native Call -> DialogJoyStick.OnRelease_JumpButton");
    return true;
}

} // namespace dta::native
