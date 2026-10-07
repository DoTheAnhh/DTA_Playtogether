#include "AntiDebug.hpp"
#include <windows.h>
#include <intrin.h>
#include <chrono>

namespace dta::security {

AntiDebug& AntiDebug::Instance() {
    static AntiDebug instance;
    return instance;
}

AntiDebug::~AntiDebug() {
    StopProtection();
}

bool AntiDebug::CheckNow() {
    // 1. Windows API Debugger Check
    if (IsDebuggerPresent()) {
        return true;
    }

    BOOL isRemotePresent = FALSE;
    CheckRemoteDebuggerPresent(GetCurrentProcess(), &isRemotePresent);
    if (isRemotePresent) {
        return true;
    }

    // 2. Hardware Breakpoint Check (DR0 - DR3)
    CONTEXT ctx = { 0 };
    ctx.ContextFlags = CONTEXT_DEBUG_REGISTERS;
    HANDLE hThread = GetCurrentThread();
    if (GetThreadContext(hThread, &ctx)) {
        if (ctx.Dr0 != 0 || ctx.Dr1 != 0 || ctx.Dr2 != 0 || ctx.Dr3 != 0) {
            return true;
        }
    }

    // 3. Timing Check via __rdtsc
    uint64_t t1 = __rdtsc();
    volatile int dummy = 0;
    for (int i = 0; i < 100; ++i) {
        dummy += i;
    }
    (void)dummy;
    uint64_t t2 = __rdtsc();
    if ((t2 - t1) > 1000000ULL) { // Massive single-stepping delay
        return true;
    }

    return false;
}

void AntiDebug::StartProtection() {
    if (m_running.exchange(true)) return;

    m_worker = std::jthread([this](std::stop_token st) {
        while (!st.stop_requested() && m_running.load()) {
            if (CheckNow()) {
                // Exit silently or terminate process on tamper
                ExitProcess(0);
            }
            std::this_thread::sleep_for(std::chrono::milliseconds(500));
        }
    });
}

void AntiDebug::StopProtection() {
    m_running.store(false);
    if (m_worker.joinable()) {
        m_worker.request_stop();
    }
}

} // namespace dta::security
