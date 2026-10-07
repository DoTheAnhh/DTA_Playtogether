#pragma once
#include <cstdint>
#include <unordered_map>
#include <vector>
#include "MemoryService.hpp"

namespace dta::memory {

class Decryptor {
public:
    // CodeStage ObscuredInt decoding: (hiddenValue - currentCryptoKey) ^ currentCryptoKey
    static int32_t DecodeObscuredInt(int32_t hiddenValue, uint32_t cryptoKey) noexcept;

    // CodeStage ObscuredFloat decoding
    static float DecodeObscuredFloat(uint32_t hiddenValue, uint32_t cryptoKey) noexcept;

    // PT_Encrypt.EncryptInt decoding from memory storage
    static int32_t DecodeEncryptInt(uint32_t key, int32_t randValue, MemoryService& mem, uintptr_t storageDicPtr);

    // Read and unpack IL2CPP Dictionary<uint32_t, uintptr_t>
    static std::unordered_map<uint32_t, uintptr_t> UnpackDictionaryU32Ptr(MemoryService& mem, uintptr_t dictPtr);
};

} // namespace dta::memory
