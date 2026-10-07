#include "TokenBucketLimiter.hpp"

namespace dta::server::security {

TokenBucketLimiter& TokenBucketLimiter::Instance() {
    static TokenBucketLimiter instance;
    return instance;
}

bool TokenBucketLimiter::AllowRequest(const std::string& clientIp, double ratePerMin, double maxBurst) {
    std::lock_guard<std::mutex> lock(m_mutex);
    auto now = std::chrono::steady_clock::now();
    auto& bucket = m_buckets[clientIp];

    double elapsedSeconds = std::chrono::duration<double>(now - bucket.lastRefill).count();
    bucket.lastRefill = now;

    // Refill
    bucket.tokens += elapsedSeconds * (ratePerMin / 60.0);
    if (bucket.tokens > maxBurst) {
        bucket.tokens = maxBurst;
    }

    if (bucket.tokens >= 1.0) {
        bucket.tokens -= 1.0;
        return true;
    }

    return false;
}

} // namespace dta::server::security
