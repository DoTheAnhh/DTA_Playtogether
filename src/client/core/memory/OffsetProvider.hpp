#pragma once
#include <string>
#include <unordered_map>
#include <cstdint>

namespace dta::memory {

struct NativeOffsets {
    // RVAs from libil2cpp.so base
    uintptr_t SetTransientPosition{0x52F2B20};
    uintptr_t ConnectToZoneMove{0x5A13410};
    uintptr_t FishingPoleOnClick{0x4EBD72C};
    uintptr_t PickaxOnClick{0x597829C};
    uintptr_t ShovelOnClick{0x5978AF4};
    uintptr_t InsectNetOnClick{0x4F77F60};
    uintptr_t AttachActionOnClick{0x4CEB6A8};
    uintptr_t JoystickJumpPress{0x5E70584};
    uintptr_t JoystickJumpRelease{0x5E708D8};
    uintptr_t UiButtonClick{0x524D90C};
    uintptr_t DialogBoxMessageOk{0x5A29F80};
    uintptr_t DialogBoxQuestionOk{0x5A2A0F0};
    uintptr_t DialogBoxQuestionCancel{0x5A2A170};
    uintptr_t DialogRewardPopupYes{0x5A31C00};
    uintptr_t ResultItemSkip{0x4D370D8};
    uintptr_t ResultItemClose{0x4D372CC};
    uintptr_t FishingKeepFish{0x5DE6BB4};
    uintptr_t FishingSellFish{0x5DE9C7C};
    uintptr_t FishingOpenBox{0x5DE9FF8};
    uintptr_t ItemRepairClick{0x5E6AC7C};
    uintptr_t ItemRepairClose{0x5E6AD68};
    uintptr_t PickFieldObject{0x57CE884};
    uintptr_t FarmInteract{0x51E2A40};
    uintptr_t FarmPlantSeed{0x51E2DC8};
    uintptr_t FarmHarvest{0x51E3100};
};

struct FieldOffsets {
    // FrameWork fields
    uint32_t fwSysDialog{32};
    uint32_t fwSysLayer{40};
    uint32_t fwSysActor{112};
    uint32_t fwSysFishing{200};
    uint32_t fwSysInsect{280};
    uint32_t fwSysCollect{288};

    // ActorSystem
    uint32_t actorSystemMyCharacter{88};

    // ActorCharacter
    uint32_t actorControl{104};
    uint32_t actorBaseTransform{80};
    uint32_t actorCacheDialogActionBtn{680};

    // KinematicCharacterMotor
    uint32_t motorTransientPosition{0x100};

    // ActorDefaultControl
    uint32_t ctrlFishingState{0x118};
    uint32_t ctrlCatchFishId{0x128};
    uint32_t ctrlCatchFishSize{0x12C};
    uint32_t ctrlHiddenLevel{0x130};
    uint32_t ctrlPickaxState{0x140};
    uint32_t ctrlExcavateState{0x148};
    uint32_t ctrlCollectState{0x150};
};

class OffsetProvider {
public:
    static OffsetProvider& Instance();

    void LoadDefaults();
    bool LoadFromFile(const std::string& filePath);
    void UpdateFromJson(const std::string& jsonContent);

    [[nodiscard]] const NativeOffsets& GetNative() const { return m_native; }
    [[nodiscard]] const FieldOffsets&  GetField() const  { return m_field; }

private:
    OffsetProvider();
    NativeOffsets m_native;
    FieldOffsets  m_field;
};

} // namespace dta::memory
