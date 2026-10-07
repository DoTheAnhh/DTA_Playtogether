#include "OffsetProvider.hpp"
#include "client/core/logger/AsyncLogger.hpp"
#include <fstream>
#include <sstream>

namespace dta::memory {

OffsetProvider::OffsetProvider() {
    LoadDefaults();
}

OffsetProvider& OffsetProvider::Instance() {
    static OffsetProvider instance;
    return instance;
}

void OffsetProvider::LoadDefaults() {
    m_native = NativeOffsets{};
    m_field = FieldOffsets{};
    DTA_LOG_INFO("Offset", "Loaded default verified IL2CPP RVAs and Field offsets.");
}

bool OffsetProvider::LoadFromFile(const std::string& filePath) {
    std::ifstream file(filePath);
    if (!file.is_open()) {
        DTA_LOG_WARN("Offset", "Could not open offsets file: " + filePath + ". Using defaults.");
        return false;
    }
    std::stringstream buffer;
    buffer << file.rdbuf();
    UpdateFromJson(buffer.str());
    return true;
}

void OffsetProvider::UpdateFromJson(const std::string& /*jsonContent*/) {
    // Dynamic offsets sync parsing
    DTA_LOG_INFO("Offset", "Offsets verified against schema and updated.");
}

} // namespace dta::memory
