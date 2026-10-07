#include "DatabaseManager.hpp"
#include <chrono>

namespace dta::server::database {

DatabaseManager& DatabaseManager::Instance() {
    static DatabaseManager instance;
    return instance;
}

void DatabaseManager::Init() {
    std::lock_guard<std::mutex> lock(m_mutex);
    // Seed default VIP test key
    UserRecord defaultVip;
    defaultVip.licenseKey = "DTA-VIP-2026-KEY";
    defaultVip.boundHWID = "";
    defaultVip.expireTimestampMs = 1893456000000ULL; // Year 2030
    defaultVip.userTier = 1;
    defaultVip.isActive = true;
    m_users[defaultVip.licenseKey] = defaultVip;
}

bool DatabaseManager::ValidateKey(const std::string& key, const std::string& hwid, UserRecord& outRecord) {
    std::lock_guard<std::mutex> lock(m_mutex);
    auto it = m_users.find(key);
    if (it == m_users.end()) return false;

    auto& rec = it->second;
    if (!rec.isActive) return false;

    // Check expiration
    auto nowMs = static_cast<uint64_t>(std::chrono::duration_cast<std::chrono::milliseconds>(
        std::chrono::system_clock::now().time_since_epoch()).count());
    if (rec.expireTimestampMs < nowMs) return false;

    // First time binding
    if (rec.boundHWID.empty()) {
        rec.boundHWID = hwid;
    } else if (rec.boundHWID != hwid) {
        return false; // HWID mismatch!
    }

    outRecord = rec;
    return true;
}

bool DatabaseManager::RegisterKey(const UserRecord& record) {
    std::lock_guard<std::mutex> lock(m_mutex);
    m_users[record.licenseKey] = record;
    return true;
}

std::vector<UserRecord> DatabaseManager::GetAllKeys() {
    std::lock_guard<std::mutex> lock(m_mutex);
    std::vector<UserRecord> list;
    list.reserve(m_users.size());
    for (const auto& [k, v] : m_users) {
        list.push_back(v);
    }
    return list;
}

bool DatabaseManager::CreateKey(const std::string& key, int daysValid, uint32_t tier) {
    if (key.empty()) return false;
    std::lock_guard<std::mutex> lock(m_mutex);
    auto nowMs = static_cast<uint64_t>(std::chrono::duration_cast<std::chrono::milliseconds>(
        std::chrono::system_clock::now().time_since_epoch()).count());
    uint64_t expireMs = nowMs + static_cast<uint64_t>(daysValid) * 86400000ULL;

    UserRecord rec;
    rec.licenseKey = key;
    rec.boundHWID = "";
    rec.expireTimestampMs = expireMs;
    rec.userTier = tier;
    rec.isActive = true;
    m_users[key] = rec;
    return true;
}

bool DatabaseManager::DeleteKey(const std::string& key) {
    std::lock_guard<std::mutex> lock(m_mutex);
    return m_users.erase(key) > 0;
}

bool DatabaseManager::ResetHWID(const std::string& key) {
    std::lock_guard<std::mutex> lock(m_mutex);
    auto it = m_users.find(key);
    if (it != m_users.end()) {
        it->second.boundHWID = "";
        return true;
    }
    return false;
}

} // namespace dta::server::database
