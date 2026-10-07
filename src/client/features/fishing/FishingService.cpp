#include "FishingService.hpp"
#include "client/core/memory/OffsetProvider.hpp"
#include "client/core/memory/Decryptor.hpp"
#include "client/core/logger/AsyncLogger.hpp"

namespace dta::features::fishing {

FishingService::FishingService(std::shared_ptr<memory::MemoryService> memory,
                               std::shared_ptr<native::GameActionDispatcher> dispatcher)
    : m_memory(std::move(memory)), m_dispatcher(std::move(dispatcher)) {}

bool FishingService::CastRod() {
    if (!m_dispatcher) return false;
    return m_dispatcher->ClickFishing(m_rodPtr);
}

bool FishingService::ReelIn() {
    if (!m_dispatcher) return false;
    return m_dispatcher->ClickFishing(m_rodPtr);
}

bool FishingService::KeepFish() {
    if (!m_dispatcher) return false;
    return m_dispatcher->KeepFish(m_dialogPtr);
}

bool FishingService::SellFish() {
    if (!m_dispatcher) return false;
    return m_dispatcher->SellFish(m_dialogPtr);
}

bool FishingService::OpenBox() {
    if (!m_dispatcher) return false;
    return m_dispatcher->OpenFishBox(m_dialogPtr);
}

bool FishingService::RepairRod() {
    if (!m_dispatcher) return false;
    return m_dispatcher->RepairItem(m_dialogPtr);
}

FishingState FishingService::GetFishingState() {
    if (!m_memory || !m_controlPtr) return FishingState::IDLE;

    uint32_t stateOffset = memory::OffsetProvider::Instance().GetField().ctrlFishingState;
    // CodeStage ObscuredInt {hash, hiddenValue, currentCryptoKey, fakeValue}
    // hiddenValue at offset +4, key at offset +8
    int32_t hiddenVal = m_memory->ReadI32(m_controlPtr + stateOffset + 4);
    uint32_t cryptoKey = m_memory->ReadU32(m_controlPtr + stateOffset + 8);

    int32_t rawState = memory::Decryptor::DecodeObscuredInt(hiddenVal, cryptoKey);
    if (rawState < 0 || rawState > 64) {
        return FishingState::IDLE;
    }

    return static_cast<FishingState>(rawState);
}

std::optional<FishModel> FishingService::GetCurrentFishInfo() {
    if (!m_memory || !m_controlPtr) return std::nullopt;

    uint32_t catchIdOffset = memory::OffsetProvider::Instance().GetField().ctrlCatchFishId;
    uint32_t fishId = m_memory->ReadU32(m_controlPtr + catchIdOffset);

    if (fishId == 0) {
        // Try reading hidden level
        uint32_t hiddenLevelOffset = memory::OffsetProvider::Instance().GetField().ctrlHiddenLevel;
        uint32_t levelKey = m_memory->ReadU32(m_controlPtr + hiddenLevelOffset);
        int32_t levelRand = m_memory->ReadI32(m_controlPtr + hiddenLevelOffset + 4);

        if (levelKey != 0 && m_storageDicPtr != 0) {
            int32_t decodedId = memory::Decryptor::DecodeEncryptInt(levelKey, levelRand, *m_memory, m_storageDicPtr);
            if (decodedId > 0 && decodedId < 100000000) {
                fishId = static_cast<uint32_t>(decodedId);
            }
        }
    }

    if (fishId != 0) {
        const auto* catalogItem = FishingCatalog::Instance().FindFish(fishId);
        if (catalogItem) {
            return *catalogItem;
        }
        FishModel fallback;
        fallback.fishId = fishId;
        fallback.shadowTier = 3;
        fallback.name = "Cá #" + std::to_string(fishId);
        return fallback;
    }

    return std::nullopt;
}

bool FishingService::IsRodBroken() {
    // If repair dialog is open or durability is 0
    return false;
}

bool FishingService::IsResultDialogOpen() {
    return m_dialogPtr != 0;
}

} // namespace dta::features::fishing
