#pragma once
#include <string>
#include <atomic>
#include <thread>
#include <chrono>
#include <memory>
#include "shared/PacketProtocol.hpp"

namespace dta::network {

class NetworkClient {
public:
    static NetworkClient& Instance();

    void Init(const std::string& serverEndpoint);
    void Shutdown();

    void SetServerEndpoint(const std::string& endpoint) { m_serverEndpoint = endpoint; }
    [[nodiscard]] const std::string& GetServerEndpoint() const noexcept { return m_serverEndpoint; }

    bool Authenticate(const std::string& licenseKey, const std::string& hwid);
    bool ActivateLicense(const std::string& licenseKey, const std::string& hwid, std::string& outMsg);
    
    std::string HttpGet(const std::string& path);
    std::string HttpPost(const std::string& path, const std::string& jsonBody);

    std::string FetchUISchema();
    std::string FetchMasterTeleportSpots();

    void StartHeartbeat();
    void StopHeartbeat();

    [[nodiscard]] bool IsAuthenticated() const noexcept { return m_authenticated.load(); }
    [[nodiscard]] const std::string& GetSessionToken() const noexcept { return m_sessionToken; }
    [[nodiscard]] const std::string& GetActiveKey() const noexcept { return m_activeKey; }

private:
    NetworkClient() = default;
    ~NetworkClient();

    std::string m_serverEndpoint{"127.0.0.1:28445"};
    std::string m_sessionToken;
    std::string m_activeKey{"DTA-VIP-2026-KEY"};
    std::atomic<bool> m_authenticated{false};
    std::atomic<bool> m_heartbeatRunning{false};
    std::jthread m_heartbeatWorker;

    bool SendPacket(const PacketHeader& header, std::span<const uint8_t> payload);
};

} // namespace dta::network
