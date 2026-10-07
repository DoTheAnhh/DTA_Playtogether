#include "Decryptor.hpp"
#include <cstring>

namespace dta::memory {

int32_t Decryptor::DecodeObscuredInt(int32_t hiddenValue, uint32_t cryptoKey) noexcept {
    uint32_t diff = static_cast<uint32_t>(hiddenValue) - cryptoKey;
    return static_cast<int32_t>(diff ^ cryptoKey);
}

float Decryptor::DecodeObscuredFloat(uint32_t hiddenValue, uint32_t cryptoKey) noexcept {
    uint32_t valU32 = hiddenValue ^ cryptoKey;
    float result = 0.0f;
    std::memcpy(&result, &valU32, sizeof(float));
    return result;
}

std::unordered_map<uint32_t, uintptr_t> Decryptor::UnpackDictionaryU32Ptr(MemoryService& mem, uintptr_t dictPtr) {
    std::unordered_map<uint32_t, uintptr_t> result;
    if (!mem.IsValidPointer(dictPtr)) return result;

    // Dictionary<TKey, TValue> fields:
    // +0x18: _entries pointer
    // +0x20: _count int32
    uintptr_t entriesPtr = mem.ReadU64(dictPtr + 0x18);
    int32_t count = mem.ReadI32(dictPtr + 0x20);

    if (!mem.IsValidPointer(entriesPtr) || count <= 0 || count > 50000) {
        return result;
    }

    // Entries array: elements start at entriesPtr + 0x20
    // Each Entry layout: hashCode(4), next(4), key(4 or 8), padding, value(8) -> total 24 bytes
    size_t totalBytes = static_cast<size_t>(count) * 24;
    std::vector<uint8_t> buffer = mem.ReadBytes(entriesPtr + 0x20, totalBytes);
    if (buffer.size() < totalBytes) return result;

    for (int32_t i = 0; i < count; ++i) {
        const uint8_t* entry = buffer.data() + (i * 24);
        int32_t hashCode = *reinterpret_cast<const int32_t*>(entry);
        if (hashCode >= 0) {
            uint32_t key = *reinterpret_cast<const uint32_t*>(entry + 8);
            uintptr_t val = *reinterpret_cast<const uintptr_t*>(entry + 16);
            if (val != 0) {
                result[key] = val;
            }
        }
    }

    return result;
}

int32_t Decryptor::DecodeEncryptInt(uint32_t key, int32_t randValue, MemoryService& mem, uintptr_t storageDicPtr) {
    if (!mem.IsValidPointer(storageDicPtr)) return 0;

    auto dict = UnpackDictionaryU32Ptr(mem, storageDicPtr);
    auto it = dict.find(key);
    if (it == dict.end()) return 0;

    uintptr_t holder = it->second;
    if (!mem.IsValidPointer(holder)) return 0;

    // Storage<int>:
    // +0x10: _encryptAry int[]
    // +0x18: key int16
    uintptr_t valuesAry = mem.ReadU64(holder + 0x10);
    int16_t storageKey = mem.Read<int16_t>(holder + 0x18);

    if (!mem.IsValidPointer(valuesAry)) return 0;

    // IL2CPP Array layout:
    // +0x18: length int64/int32
    // +0x20: elements int32[]
    int64_t length = mem.Read<int64_t>(valuesAry + 0x18);
    if (length <= 0 || length > 4096) return 0;

    int32_t idx = static_cast<int32_t>(storageKey % length);
    if (idx < 0) idx += static_cast<int32_t>(length);

    uintptr_t elemAddr = valuesAry + 0x20 + (idx * sizeof(int32_t));
    int32_t itemVal = mem.ReadI32(elemAddr);
    return itemVal - randValue;
}

} // namespace dta::memory
