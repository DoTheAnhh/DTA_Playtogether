#pragma once
#include <string>
#include <unordered_map>
#include <mutex>
#include <cstdint>

namespace dta::server::database {

struct UserRecord {
    std::string licenseKey;
    std::string boundHWID;
    uint64_t expireTimestampMs{0};
    uint32_t userTier{1}; // 1 = VIP
    bool isActive{true};
};

class DatabaseManager {
public:
    static DatabaseManager& Instance();

    void Init();
    bool ValidateKey(const std::string& key, const std::string& hwid, UserRecord& outRecord);
    bool RegisterKey(const UserRecord& record);
    std::vector<UserRecord> GetAllKeys();
    bool CreateKey(const std::string& key, int daysValid, uint32_t tier = 1);
    bool DeleteKey(const std::string& key);
    bool ResetHWID(const std::string& key);

private:
    DatabaseManager() = default;
    std::mutex m_mutex;
    std::unordered_map<std::string, UserRecord> m_users;
};

} // namespace dta::server::database
