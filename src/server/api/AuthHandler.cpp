#include "AuthHandler.hpp"
#include <chrono>

namespace dta::server::api {

AuthHandler& AuthHandler::Instance() {
    static AuthHandler instance;
    return instance;
}

AuthResponse AuthHandler::HandleAuth(const std::string& clientIp, const AuthRequest& req) {
    AuthResponse resp;

    // 1. Rate limiter check
    if (!security::TokenBucketLimiter::Instance().AllowRequest(clientIp)) {
        resp.success = false;
        resp.message = "Rate limit exceeded. Try again in 1 minute.";
        return resp;
    }

    // 2. Database validation
    database::UserRecord user;
    if (!database::DatabaseManager::Instance().ValidateKey(req.licenseKey, req.hwid, user)) {
        resp.success = false;
        resp.message = "Invalid or expired license key / HWID mismatch.";
        return resp;
    }

    resp.success = true;
    resp.message = "Welcome to DTA PlayTogether VIP!";
    resp.expireTime = user.expireTimestampMs;
    resp.userTier = user.userTier;
    resp.sessionToken = "token_" + req.hwid.substr(0, 8);
    return resp;
}

HeartbeatResponse AuthHandler::HandleHeartbeat(const HeartbeatRequest& /*req*/) {
    HeartbeatResponse resp;
    resp.acknowledged = true;
    resp.serverTimestamp = static_cast<uint64_t>(std::chrono::duration_cast<std::chrono::milliseconds>(
        std::chrono::system_clock::now().time_since_epoch()).count());
    resp.forceDisconnect = false;
    return resp;
}

} // namespace dta::server::api
