#pragma once
#include <string>
#include <unordered_map>
#include <mutex>
#include <chrono>

namespace dta::server::security {

struct Bucket {
    double tokens{5.0};
    std::chrono::steady_clock::time_point lastRefill{std::chrono::steady_clock::now()};
};

class TokenBucketLimiter {
public:
    static TokenBucketLimiter& Instance();

    bool AllowRequest(const std::string& clientIp, double ratePerMin = 5.0, double maxBurst = 5.0);

private:
    TokenBucketLimiter() = default;
    std::mutex m_mutex;
    std::unordered_map<std::string, Bucket> m_buckets;
};

} // namespace dta::server::security
