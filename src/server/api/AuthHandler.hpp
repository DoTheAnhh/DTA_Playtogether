#pragma once
#include "shared/PacketProtocol.hpp"
#include "server/database/DatabaseManager.hpp"
#include "server/security/TokenBucketLimiter.hpp"

namespace dta::server::api {

class AuthHandler {
public:
    static AuthHandler& Instance();

    AuthResponse HandleAuth(const std::string& clientIp, const AuthRequest& req);
    HeartbeatResponse HandleHeartbeat(const HeartbeatRequest& req);

private:
    AuthHandler() = default;
};

} // namespace dta::server::api
