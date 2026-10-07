#pragma once
#include <string>
#include <string_view>
#include <chrono>
#include <vector>
#include <atomic>
#include <thread>
#include <mutex>
#include <fstream>
#include <iostream>

namespace dta::logger {

enum class LogLevel : uint8_t {
    LOG_DEBUG,
    LOG_INFO,
    LOG_WARN,
    LOG_ERROR
};

struct LogEntry {
    LogLevel level{LogLevel::LOG_INFO};
    std::string moduleTag;
    std::string message;
    std::chrono::system_clock::time_point timestamp;
};

class AsyncLogger {
public:
    static AsyncLogger& Instance();

    void Init(const std::string& logFilePath = "");
    void Shutdown();

    void Log(LogLevel level, std::string_view moduleTag, std::string_view message);

    void Debug(std::string_view moduleTag, std::string_view message) {
        Log(LogLevel::LOG_DEBUG, moduleTag, message);
    }
    void Info(std::string_view moduleTag, std::string_view message) {
        Log(LogLevel::LOG_INFO, moduleTag, message);
    }
    void Warn(std::string_view moduleTag, std::string_view message) {
        Log(LogLevel::LOG_WARN, moduleTag, message);
    }
    void Error(std::string_view moduleTag, std::string_view message) {
        Log(LogLevel::LOG_ERROR, moduleTag, message);
    }

    std::vector<LogEntry> GetRecentLogs(size_t maxCount = 50);

private:
    AsyncLogger() = default;
    ~AsyncLogger();

    std::vector<LogEntry> m_buffer;
    std::mutex m_mutex;
    std::ofstream m_fileStream;
    std::atomic<bool> m_initialized{false};
};

} // namespace dta::logger

#define DTA_LOG_DEBUG(mod, msg) ::dta::logger::AsyncLogger::Instance().Debug(mod, msg)
#define DTA_LOG_INFO(mod, msg)  ::dta::logger::AsyncLogger::Instance().Info(mod, msg)
#define DTA_LOG_WARN(mod, msg)  ::dta::logger::AsyncLogger::Instance().Warn(mod, msg)
#define DTA_LOG_ERROR(mod, msg) ::dta::logger::AsyncLogger::Instance().Error(mod, msg)
