#include "MEmuDriver.hpp"
#include "client/core/logger/AsyncLogger.hpp"
#include <tlhelp32.h>

namespace dta::device {

MEmuDriver::MEmuDriver() = default;

MEmuDriver::~MEmuDriver() {
    Detach();
}

bool MEmuDriver::FindProcessByName(const wchar_t* processName) {
    HANDLE hSnap = CreateToolhelp32Snapshot(TH32CS_SNAPPROCESS, 0);
    if (hSnap == INVALID_HANDLE_VALUE) return false;

    PROCESSENTRY32W pe32;
    pe32.dwSize = sizeof(pe32);

    if (Process32FirstW(hSnap, &pe32)) {
        do {
            if (_wcsicmp(pe32.szExeFile, processName) == 0) {
                m_pid = pe32.th32ProcessID;
                CloseHandle(hSnap);
                return true;
            }
        } while (Process32NextW(hSnap, &pe32));
    }

    CloseHandle(hSnap);
    return false;
}

void MEmuDriver::RefreshModules() {
    m_moduleCache.clear();
    if (!m_pid) return;

    HANDLE hSnap = CreateToolhelp32Snapshot(TH32CS_SNAPMODULE | TH32CS_SNAPMODULE32, m_pid);
    if (hSnap == INVALID_HANDLE_VALUE) return;

    MODULEENTRY32W me32;
    me32.dwSize = sizeof(me32);

    if (Module32FirstW(hSnap, &me32)) {
        do {
            char nameA[MAX_MODULE_NAME32 + 1] = { 0 };
            WideCharToMultiByte(CP_UTF8, 0, me32.szModule, -1, nameA, sizeof(nameA), nullptr, nullptr);
            m_moduleCache[nameA] = reinterpret_cast<uintptr_t>(me32.modBaseAddr);
        } while (Module32NextW(hSnap, &me32));
    }

    CloseHandle(hSnap);
}

bool MEmuDriver::DetectAndAttach() {
    Detach();

    if (!FindProcessByName(L"MEmuConsole.exe") && !FindProcessByName(L"MEmuHeadless.exe")) {
        return false;
    }

    m_hProcess = OpenProcess(PROCESS_VM_READ | PROCESS_VM_WRITE | PROCESS_VM_OPERATION | PROCESS_QUERY_INFORMATION, FALSE, m_pid);
    if (!m_hProcess) {
        DTA_LOG_ERROR("Device", "Failed to OpenProcess for MEmu pid " + std::to_string(m_pid));
        return false;
    }

    RefreshModules();
    DTA_LOG_INFO("Device", "Successfully attached to MEmu (PID: " + std::to_string(m_pid) + ")");
    return true;
}

void MEmuDriver::Detach() {
    if (m_hProcess) {
        CloseHandle(m_hProcess);
        m_hProcess = nullptr;
    }
    m_pid = 0;
    m_moduleCache.clear();
}

bool MEmuDriver::IsProcessAlive() const {
    if (!m_hProcess) return false;
    DWORD exitCode = 0;
    if (GetExitCodeProcess(m_hProcess, &exitCode)) {
        return exitCode == STILL_ACTIVE;
    }
    return false;
}

uintptr_t MEmuDriver::GetModuleBase(std::string_view moduleName) {
    auto it = m_moduleCache.find(std::string(moduleName));
    if (it != m_moduleCache.end()) {
        return it->second;
    }
    RefreshModules();
    it = m_moduleCache.find(std::string(moduleName));
    return (it != m_moduleCache.end()) ? it->second : 0;
}

bool MEmuDriver::ReadMemoryRaw(uintptr_t address, void* buffer, size_t size) {
    if (!m_hProcess || !buffer || address == 0) return false;
    SIZE_T bytesRead = 0;
    return ReadProcessMemory(m_hProcess, reinterpret_cast<LPCVOID>(address), buffer, size, &bytesRead) && (bytesRead == size);
}

bool MEmuDriver::WriteMemoryRaw(uintptr_t address, const void* buffer, size_t size) {
    if (!m_hProcess || !buffer || address == 0) return false;
    SIZE_T bytesWritten = 0;
    return WriteProcessMemory(m_hProcess, reinterpret_cast<LPVOID>(address), buffer, size, &bytesWritten) && (bytesWritten == size);
}

} // namespace dta::device
