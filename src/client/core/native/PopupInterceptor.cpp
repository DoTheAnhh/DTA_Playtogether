#include "PopupInterceptor.hpp"
#include "client/core/logger/AsyncLogger.hpp"

namespace dta::native {

PopupInterceptor& PopupInterceptor::Instance() {
    static PopupInterceptor instance;
    return instance;
}

void PopupInterceptor::Init(std::shared_ptr<memory::MemoryService> memory) {
    m_memory = std::move(memory);
    DTA_LOG_INFO("Interceptor", "PopupInterceptor initialized.");
}

void PopupInterceptor::CheckAndHandlePopups() {
    if (!m_memory) return;
    // Inspect active dialogs in sysDialog
    // If whitelisted popup is open, trigger corresponding action
}

} // namespace dta::native
