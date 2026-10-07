#pragma once
#include <string>
#include <vector>
#include <unordered_set>
#include <memory>
#include "client/core/memory/MemoryService.hpp"
#include "GameActionDispatcher.hpp"

namespace dta::native {

class PopupInterceptor {
public:
    static PopupInterceptor& Instance();

    void Init(std::shared_ptr<memory::MemoryService> memory);
    void CheckAndHandlePopups();

    void EnableAutoRepair(bool enable) { m_autoRepair = enable; }
    void EnableAutoSkip(bool enable) { m_autoSkip = enable; }
    void EnableAutoCloseDialogs(bool enable) { m_autoCloseDialogs = enable; }

private:
    PopupInterceptor() = default;
    std::shared_ptr<memory::MemoryService> m_memory;

    bool m_autoRepair{true};
    bool m_autoSkip{true};
    bool m_autoCloseDialogs{true};
};

} // namespace dta::native
