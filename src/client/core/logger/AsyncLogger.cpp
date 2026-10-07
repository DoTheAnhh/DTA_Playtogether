#include "AsyncLogger.hpp"
#include <iomanip>
#include <sstream>
#if defined(_WIN32)
#define WIN32_LEAN_AND_MEAN
#include <windows.h>
#endif

namespace dta::logger {

AsyncLogger& AsyncLogger::Instance() {
    static AsyncLogger instance;
    return instance;
}

AsyncLogger::~AsyncLogger() {
    Shutdown();
}

void AsyncLogger::Init(const std::string& /*logFilePath*/) {
    std::lock_guard<std::mutex> lock(m_mutex);
    // TUYỆT ĐỐI KHÔNG TẠO HOẶC MỞ FILE TRÊN ĐĨA - 100% Zero-Disk-Log
    m_initialized.store(true);
}

void AsyncLogger::Shutdown() {
    std::lock_guard<std::mutex> lock(m_mutex);
    m_initialized.store(false);
}

void AsyncLogger::Log(LogLevel level, std::string_view moduleTag, std::string_view message) {
    auto now = std::chrono::system_clock::now();
    LogEntry entry{level, std::string(moduleTag), std::string(message), now};

    // Console output format
    const char* lvlStr = "INFO";
    switch (level) {
        case LogLevel::LOG_DEBUG: lvlStr = "DEBUG"; break;
        case LogLevel::LOG_INFO:  lvlStr = "INFO";  break;
        case LogLevel::LOG_WARN:  lvlStr = "WARN";  break;
        case LogLevel::LOG_ERROR: lvlStr = "ERROR"; break;
    }

    auto timeT = std::chrono::system_clock::to_time_t(now);
    std::tm tmNow{};
#if defined(_WIN32)
    localtime_s(&tmNow, &timeT);
#else
    localtime_r(&timeT, &tmNow);
#endif

    std::stringstream ss;
    ss << "[" << std::put_time(&tmNow, "%H:%M:%S") << "] "
       << "[" << lvlStr << "] "
       << "[DTA." << moduleTag << "] "
       << message << "\n";

    std::string formatted = ss.str();
#if defined(_WIN32)
    OutputDebugStringA(formatted.c_str());
#endif

    {
        std::lock_guard<std::mutex> lock(m_mutex);
        if (m_buffer.size() >= 500) {
            m_buffer.erase(m_buffer.begin(), m_buffer.begin() + 50);
        }
        m_buffer.push_back(std::move(entry));
        // Tuyệt đối không ghi ra đĩa file log!
    }
}

std::vector<LogEntry> AsyncLogger::GetRecentLogs(size_t maxCount) {
    std::lock_guard<std::mutex> lock(m_mutex);
    if (m_buffer.size() <= maxCount) {
        return m_buffer;
    }
    return std::vector<LogEntry>(m_buffer.end() - maxCount, m_buffer.end());
}

} // namespace dta::logger
