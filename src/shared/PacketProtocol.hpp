#pragma once
#include <cstdint>
#include <vector>
#include <string>
#include <span>
#include <cstring>
#include "Types.hpp"

namespace dta {

#pragma pack(push, 1)
struct PacketHeader {
    uint32_t magic{0x44544150}; // "DTAP"
    uint16_t packetId{0};       // Command ID
    uint32_t payloadLength{0};  // Payload size in bytes
    uint64_t timestamp{0};      // Unix millisecond timestamp
    uint8_t  nonce[12]{0};      // AES-256-GCM IV Nonce
    uint8_t  tag[16]{0};        // AES-256-GCM Auth Tag
};
#pragma pack(pop)

namespace PacketId {
    constexpr uint16_t AUTH_REQ           = 0x0001;
    constexpr uint16_t AUTH_RESP          = 0x0002;
    constexpr uint16_t HEARTBEAT_REQ      = 0x0003;
    constexpr uint16_t HEARTBEAT_RESP     = 0x0004;
    constexpr uint16_t UI_SCHEMA_REQ      = 0x0005;
    constexpr uint16_t UI_SCHEMA_RESP     = 0x0006;
    constexpr uint16_t OFFSET_SYNC_REQ    = 0x0007;
    constexpr uint16_t OFFSET_SYNC_RESP   = 0x0008;
    constexpr uint16_t WAYPOINT_SYNC_REQ  = 0x0009;
    constexpr uint16_t WAYPOINT_SYNC_RESP = 0x000A;
}

struct AuthRequest {
    std::string licenseKey;
    std::string hwid;
    std::string clientVersion{"3.0.0"};
};

struct AuthResponse {
    bool success{false};
    std::string message;
    uint64_t expireTime{0};
    uint32_t userTier{0};
    std::string sessionToken;
};

struct HeartbeatRequest {
    std::string sessionToken;
    uint32_t clientUptimeSeconds{0};
    uint32_t activeBotsMask{0};
};

struct HeartbeatResponse {
    bool acknowledged{false};
    uint64_t serverTimestamp{0};
    bool forceDisconnect{false};
};

// Serialization helper
class PacketSerializer {
public:
    static std::vector<uint8_t> Pack(uint16_t packetId, std::span<const uint8_t> payload, uint64_t timestamp = 0) {
        PacketHeader header;
        header.packetId = packetId;
        header.payloadLength = static_cast<uint32_t>(payload.size());
        header.timestamp = timestamp;

        std::vector<uint8_t> buffer(sizeof(PacketHeader) + payload.size());
        std::memcpy(buffer.data(), &header, sizeof(PacketHeader));
        if (!payload.empty()) {
            std::memcpy(buffer.data() + sizeof(PacketHeader), payload.data(), payload.size());
        }
        return buffer;
    }

    static bool Unpack(std::span<const uint8_t> raw, PacketHeader& outHeader, std::vector<uint8_t>& outPayload) {
        if (raw.size() < sizeof(PacketHeader)) return false;
        std::memcpy(&outHeader, raw.data(), sizeof(PacketHeader));
        if (outHeader.magic != 0x44544150) return false;
        if (raw.size() < sizeof(PacketHeader) + outHeader.payloadLength) return false;

        outPayload.resize(outHeader.payloadLength);
        if (outHeader.payloadLength > 0) {
            std::memcpy(outPayload.data(), raw.data() + sizeof(PacketHeader), outHeader.payloadLength);
        }
        return true;
    }
};

} // namespace dta
