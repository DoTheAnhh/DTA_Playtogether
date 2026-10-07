#pragma once
#include <windows.h>
#include <string>
#include <memory>
#include <vector>

namespace dta::server::ui {

class ServerPanelWindow {
public:
    static ServerPanelWindow& Instance();

    bool Create(HINSTANCE hInstance, int nCmdShow);
    void RunMessageLoop();
    void AppendLog(const std::string& logLine);

private:
    ServerPanelWindow();
    ~ServerPanelWindow();

    static LRESULT CALLBACK WndProc(HWND hWnd, UINT msg, WPARAM wParam, LPARAM lParam);
    LRESULT HandleMessage(HWND hWnd, UINT msg, WPARAM wParam, LPARAM lParam);

    void InitControls(HWND hWnd);
    void RefreshKeyList();
    void RefreshSpotList();
    void OnTabChanged(int newTab);

    void DrawStatCard(HDC hdc, const RECT& rc, const std::wstring& title,
                      const std::wstring& value, const std::wstring& subtitle,
                      COLORREF accentColor);

    HINSTANCE m_hInstance{nullptr};
    HWND m_hWnd{nullptr};

    // Modern Tab Switcher Buttons
    HWND m_hBtnTab0{nullptr};
    HWND m_hBtnTab1{nullptr};
    HWND m_hBtnTab2{nullptr};

    // Tab 1: Key Management Controls
    HWND m_hKeyList{nullptr};
    HWND m_hLblNewKey{nullptr};
    HWND m_hEditNewKey{nullptr};
    HWND m_hLblDays{nullptr};
    HWND m_hEditDays{nullptr};
    HWND m_hBtnCreateKey{nullptr};
    HWND m_hBtnRefreshKeys{nullptr};
    HWND m_hBtnResetHWID{nullptr};
    HWND m_hBtnDeleteKey{nullptr};

    // Tab 2: Master Teleport Controls
    HWND m_hSpotList{nullptr};
    HWND m_hLblSpotName{nullptr};
    HWND m_hEditSpotName{nullptr};
    HWND m_hLblMap{nullptr};
    HWND m_hComboMap{nullptr};
    HWND m_hLblX{nullptr};
    HWND m_hEditX{nullptr};
    HWND m_hLblY{nullptr};
    HWND m_hEditY{nullptr};
    HWND m_hLblZ{nullptr};
    HWND m_hEditZ{nullptr};
    HWND m_hBtnAddSpot{nullptr};
    HWND m_hBtnToggleSpot{nullptr};
    HWND m_hBtnDeleteSpot{nullptr};
    HWND m_hBtnResetSpots{nullptr};

    // Tab 3: Server Logs Controls
    HWND m_hEditLogs{nullptr};

    int m_currentTab{0};
    size_t m_cachedKeyCount{0};
    size_t m_cachedSpotCount{0};

    HBRUSH m_hBgBrush{nullptr};
    HBRUSH m_hCardBrush{nullptr};
    HBRUSH m_hInputBrush{nullptr};
    HFONT m_hFontTitle{nullptr};
    HFONT m_hFontRegular{nullptr};
    HFONT m_hFontBold{nullptr};
    HFONT m_hFontMono{nullptr};
    HFONT m_hFontSmall{nullptr};
};

} // namespace dta::server::ui
