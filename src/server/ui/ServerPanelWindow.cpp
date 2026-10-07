#include "ServerPanelWindow.hpp"
#include "server/database/DatabaseManager.hpp"
#include "server/services/MasterTeleportService.hpp"
#include "shared/Theme.hpp"
#include <commctrl.h>
#include <dwmapi.h>
#include <sstream>
#include <iomanip>
#include <ctime>

#pragma comment(lib, "comctl32.lib")
#pragma comment(lib, "dwmapi.lib")

namespace dta::server::ui {

#define IDC_TAB_BTN_0         1001
#define IDC_TAB_BTN_1         1002
#define IDC_TAB_BTN_2         1003

#define IDC_KEY_LIST          1010
#define IDC_EDIT_KEY_NAME     1011
#define IDC_EDIT_KEY_DAYS     1012
#define IDC_BTN_KEY_CREATE    1013
#define IDC_BTN_KEY_RESETHWID 1014
#define IDC_BTN_KEY_DELETE    1015
#define IDC_BTN_KEY_REFRESH   1016

#define IDC_SPOT_LIST         1020
#define IDC_EDIT_SPOT_NAME    1021
#define IDC_COMBO_MAP         1022
#define IDC_EDIT_SPOT_X       1023
#define IDC_EDIT_SPOT_Y       1024
#define IDC_EDIT_SPOT_Z       1025
#define IDC_BTN_SPOT_ADD      1026
#define IDC_BTN_SPOT_TOGGLE   1027
#define IDC_BTN_SPOT_DELETE   1028
#define IDC_BTN_SPOT_RESET    1029

#define IDC_EDIT_LOGS         1040

ServerPanelWindow& ServerPanelWindow::Instance() {
    static ServerPanelWindow instance;
    return instance;
}

ServerPanelWindow::ServerPanelWindow() {
    m_hBgBrush = CreateSolidBrush(theme::Color::BgMain);
    m_hCardBrush = CreateSolidBrush(theme::Color::Card);
    m_hInputBrush = CreateSolidBrush(theme::Color::InputBg);

    m_hFontTitle = CreateFontW(20, 0, 0, 0, FW_BOLD, FALSE, FALSE, FALSE,
        DEFAULT_CHARSET, OUT_DEFAULT_PRECIS, CLIP_DEFAULT_PRECIS, CLEARTYPE_QUALITY,
        DEFAULT_PITCH | FF_DONTCARE, L"Segoe UI");

    m_hFontRegular = CreateFontW(14, 0, 0, 0, FW_NORMAL, FALSE, FALSE, FALSE,
        DEFAULT_CHARSET, OUT_DEFAULT_PRECIS, CLIP_DEFAULT_PRECIS, CLEARTYPE_QUALITY,
        DEFAULT_PITCH | FF_DONTCARE, L"Segoe UI");

    m_hFontBold = CreateFontW(14, 0, 0, 0, FW_BOLD, FALSE, FALSE, FALSE,
        DEFAULT_CHARSET, OUT_DEFAULT_PRECIS, CLIP_DEFAULT_PRECIS, CLEARTYPE_QUALITY,
        DEFAULT_PITCH | FF_DONTCARE, L"Segoe UI");

    m_hFontMono = CreateFontW(13, 0, 0, 0, FW_NORMAL, FALSE, FALSE, FALSE,
        DEFAULT_CHARSET, OUT_DEFAULT_PRECIS, CLIP_DEFAULT_PRECIS, CLEARTYPE_QUALITY,
        FIXED_PITCH | FF_MODERN, L"Consolas");

    m_hFontSmall = CreateFontW(12, 0, 0, 0, FW_NORMAL, FALSE, FALSE, FALSE,
        DEFAULT_CHARSET, OUT_DEFAULT_PRECIS, CLIP_DEFAULT_PRECIS, CLEARTYPE_QUALITY,
        DEFAULT_PITCH | FF_DONTCARE, L"Segoe UI");
}

ServerPanelWindow::~ServerPanelWindow() {
    if (m_hBgBrush) DeleteObject(m_hBgBrush);
    if (m_hCardBrush) DeleteObject(m_hCardBrush);
    if (m_hInputBrush) DeleteObject(m_hInputBrush);
    if (m_hFontTitle) DeleteObject(m_hFontTitle);
    if (m_hFontRegular) DeleteObject(m_hFontRegular);
    if (m_hFontBold) DeleteObject(m_hFontBold);
    if (m_hFontMono) DeleteObject(m_hFontMono);
    if (m_hFontSmall) DeleteObject(m_hFontSmall);
}

LRESULT CALLBACK ServerPanelWindow::WndProc(HWND hWnd, UINT msg, WPARAM wParam, LPARAM lParam) {
    ServerPanelWindow* pThis = nullptr;
    if (msg == WM_NCCREATE) {
        auto cs = reinterpret_cast<CREATESTRUCT*>(lParam);
        pThis = reinterpret_cast<ServerPanelWindow*>(cs->lpCreateParams);
        SetWindowLongPtr(hWnd, GWLP_USERDATA, reinterpret_cast<LONG_PTR>(pThis));
        pThis->m_hWnd = hWnd;
    } else {
        pThis = reinterpret_cast<ServerPanelWindow*>(GetWindowLongPtr(hWnd, GWLP_USERDATA));
    }

    if (pThis) {
        return pThis->HandleMessage(hWnd, msg, wParam, lParam);
    }
    return DefWindowProcW(hWnd, msg, wParam, lParam);
}

bool ServerPanelWindow::Create(HINSTANCE hInstance, int nCmdShow) {
    m_hInstance = hInstance;

    INITCOMMONCONTROLSEX icex;
    icex.dwSize = sizeof(INITCOMMONCONTROLSEX);
    icex.dwICC = ICC_LISTVIEW_CLASSES;
    InitCommonControlsEx(&icex);

    WNDCLASSEXW wc{};
    wc.cbSize = sizeof(WNDCLASSEXW);
    wc.style = CS_HREDRAW | CS_VREDRAW;
    wc.lpfnWndProc = WndProc;
    wc.hInstance = hInstance;
    wc.hCursor = LoadCursor(nullptr, IDC_ARROW);
    wc.hbrBackground = m_hBgBrush;
    wc.lpszClassName = L"DTAServerControlPanel";

    RegisterClassExW(&wc);

    int winW = 1080;
    int winH = 720;
    int posX = (GetSystemMetrics(SM_CXSCREEN) - winW) / 2;
    int posY = (GetSystemMetrics(SM_CYSCREEN) - winH) / 2;

    m_hWnd = CreateWindowExW(
        0, L"DTAServerControlPanel",
        L"✦ DTA PLAYTOGETHER - MASTER SERVER CONTROL CENTER v3.0",
        WS_OVERLAPPEDWINDOW & ~WS_THICKFRAME & ~WS_MAXIMIZEBOX,
        posX, posY, winW, winH,
        nullptr, nullptr, hInstance, this
    );

    if (!m_hWnd) return false;

    // Enable Immersive Dark Mode for title bar & rounded corners (Win11)
    BOOL dark = TRUE;
    DwmSetWindowAttribute(m_hWnd, 20, &dark, sizeof(dark));
    DWM_WINDOW_CORNER_PREFERENCE corner = DWMWCP_ROUND;
    DwmSetWindowAttribute(m_hWnd, 33, &corner, sizeof(corner));

    InitControls(m_hWnd);
    ShowWindow(m_hWnd, nCmdShow);
    UpdateWindow(m_hWnd);

    return true;
}

void ServerPanelWindow::InitControls(HWND hWnd) {
    // 1. Modern Segmented Tab Buttons (x=20, 265, 510, y=162, h=38)
    m_hBtnTab0 = CreateWindowExW(0, L"BUTTON", L"🔑  Quản Lý License Key (VIP)",
        WS_CHILD | WS_VISIBLE | BS_OWNERDRAW,
        20, 162, 235, 38, hWnd, reinterpret_cast<HMENU>(IDC_TAB_BTN_0), m_hInstance, nullptr);

    m_hBtnTab1 = CreateWindowExW(0, L"BUTTON", L"📍  Master Teleport Spots",
        WS_CHILD | WS_VISIBLE | BS_OWNERDRAW,
        265, 162, 235, 38, hWnd, reinterpret_cast<HMENU>(IDC_TAB_BTN_1), m_hInstance, nullptr);

    m_hBtnTab2 = CreateWindowExW(0, L"BUTTON", L"📜  Nhật Ký & Trạng Thái Server",
        WS_CHILD | WS_VISIBLE | BS_OWNERDRAW,
        510, 162, 245, 38, hWnd, reinterpret_cast<HMENU>(IDC_TAB_BTN_2), m_hInstance, nullptr);

    // ==========================================
    // TAB 1 CONTROLS: KEY MANAGEMENT
    // ==========================================
    m_hKeyList = CreateWindowExW(WS_EX_CLIENTEDGE, WC_LISTVIEWW, L"",
        WS_CHILD | WS_VISIBLE | LVS_REPORT | LVS_SINGLESEL,
        34, 222, 1002, 365, hWnd, reinterpret_cast<HMENU>(IDC_KEY_LIST), m_hInstance, nullptr);
    SendMessage(m_hKeyList, WM_SETFONT, reinterpret_cast<WPARAM>(m_hFontMono), TRUE);
    ListView_SetExtendedListViewStyle(m_hKeyList, LVS_EX_FULLROWSELECT | LVS_EX_DOUBLEBUFFER);
    ListView_SetBkColor(m_hKeyList, theme::Color::CardHover);
    ListView_SetTextBkColor(m_hKeyList, theme::Color::CardHover);
    ListView_SetTextColor(m_hKeyList, theme::Color::TextPrimary);

    LVCOLUMNW lvc{};
    lvc.mask = LVCF_TEXT | LVCF_WIDTH | LVCF_SUBITEM;
    lvc.cx = 240; lvc.pszText = const_cast<LPWSTR>(L"Mã License Key"); ListView_InsertColumn(m_hKeyList, 0, &lvc);
    lvc.cx = 330; lvc.pszText = const_cast<LPWSTR>(L"HWID Thiết Bị Đã Khóa"); ListView_InsertColumn(m_hKeyList, 1, &lvc);
    lvc.cx = 180; lvc.pszText = const_cast<LPWSTR>(L"Hạn Sử Dụng"); ListView_InsertColumn(m_hKeyList, 2, &lvc);
    lvc.cx = 120; lvc.pszText = const_cast<LPWSTR>(L"Gói Quyền Hạn"); ListView_InsertColumn(m_hKeyList, 3, &lvc);
    lvc.cx = 120; lvc.pszText = const_cast<LPWSTR>(L"Trạng Thái"); ListView_InsertColumn(m_hKeyList, 4, &lvc);

    // Bottom inputs for Tab 1 (y = 600)
    m_hLblNewKey = CreateWindowExW(0, L"STATIC", L"Mã Key Mới:", WS_CHILD | WS_VISIBLE,
        36, 605, 85, 24, hWnd, nullptr, m_hInstance, nullptr);
    SendMessage(m_hLblNewKey, WM_SETFONT, reinterpret_cast<WPARAM>(m_hFontRegular), TRUE);

    m_hEditNewKey = CreateWindowExW(WS_EX_CLIENTEDGE, L"EDIT", L"DTA-VIP-2026-KEY",
        WS_CHILD | WS_VISIBLE | ES_AUTOHSCROLL,
        125, 601, 220, 28, hWnd, reinterpret_cast<HMENU>(IDC_EDIT_KEY_NAME), m_hInstance, nullptr);
    SendMessage(m_hEditNewKey, WM_SETFONT, reinterpret_cast<WPARAM>(m_hFontMono), TRUE);

    m_hLblDays = CreateWindowExW(0, L"STATIC", L"Số Ngày:", WS_CHILD | WS_VISIBLE,
        360, 605, 65, 24, hWnd, nullptr, m_hInstance, nullptr);
    SendMessage(m_hLblDays, WM_SETFONT, reinterpret_cast<WPARAM>(m_hFontRegular), TRUE);

    m_hEditDays = CreateWindowExW(WS_EX_CLIENTEDGE, L"EDIT", L"365",
        WS_CHILD | WS_VISIBLE | ES_NUMBER,
        430, 601, 60, 28, hWnd, reinterpret_cast<HMENU>(IDC_EDIT_KEY_DAYS), m_hInstance, nullptr);
    SendMessage(m_hEditDays, WM_SETFONT, reinterpret_cast<WPARAM>(m_hFontRegular), TRUE);

    m_hBtnCreateKey = CreateWindowExW(0, L"BUTTON", L"+ Tạo Key Mới",
        WS_CHILD | WS_VISIBLE | BS_OWNERDRAW,
        505, 599, 125, 32, hWnd, reinterpret_cast<HMENU>(IDC_BTN_KEY_CREATE), m_hInstance, nullptr);

    m_hBtnResetHWID = CreateWindowExW(0, L"BUTTON", L"🔄 Reset HWID",
        WS_CHILD | WS_VISIBLE | BS_OWNERDRAW,
        640, 599, 160, 32, hWnd, reinterpret_cast<HMENU>(IDC_BTN_KEY_RESETHWID), m_hInstance, nullptr);

    m_hBtnDeleteKey = CreateWindowExW(0, L"BUTTON", L"🗑 Xóa Key",
        WS_CHILD | WS_VISIBLE | BS_OWNERDRAW,
        810, 599, 100, 32, hWnd, reinterpret_cast<HMENU>(IDC_BTN_KEY_DELETE), m_hInstance, nullptr);

    m_hBtnRefreshKeys = CreateWindowExW(0, L"BUTTON", L"↻ Làm Mới",
        WS_CHILD | WS_VISIBLE | BS_OWNERDRAW,
        920, 599, 95, 32, hWnd, reinterpret_cast<HMENU>(IDC_BTN_KEY_REFRESH), m_hInstance, nullptr);

    // ==========================================
    // TAB 2 CONTROLS: MASTER TELEPORT SPOTS
    // ==========================================
    m_hSpotList = CreateWindowExW(WS_EX_CLIENTEDGE, WC_LISTVIEWW, L"",
        WS_CHILD | LVS_REPORT | LVS_SINGLESEL,
        34, 222, 1002, 365, hWnd, reinterpret_cast<HMENU>(IDC_SPOT_LIST), m_hInstance, nullptr);
    SendMessage(m_hSpotList, WM_SETFONT, reinterpret_cast<WPARAM>(m_hFontRegular), TRUE);
    ListView_SetExtendedListViewStyle(m_hSpotList, LVS_EX_FULLROWSELECT | LVS_EX_DOUBLEBUFFER);
    ListView_SetBkColor(m_hSpotList, theme::Color::CardHover);
    ListView_SetTextBkColor(m_hSpotList, theme::Color::CardHover);
    ListView_SetTextColor(m_hSpotList, theme::Color::TextPrimary);

    lvc.cx = 260; lvc.pszText = const_cast<LPWSTR>(L"Tên Điểm Dịch Chuyển"); ListView_InsertColumn(m_hSpotList, 0, &lvc);
    lvc.cx = 140; lvc.pszText = const_cast<LPWSTR>(L"Bản Đồ (Map)"); ListView_InsertColumn(m_hSpotList, 1, &lvc);
    lvc.cx = 110; lvc.pszText = const_cast<LPWSTR>(L"Tọa Độ X"); ListView_InsertColumn(m_hSpotList, 2, &lvc);
    lvc.cx = 110; lvc.pszText = const_cast<LPWSTR>(L"Tọa Độ Y"); ListView_InsertColumn(m_hSpotList, 3, &lvc);
    lvc.cx = 110; lvc.pszText = const_cast<LPWSTR>(L"Tọa Độ Z"); ListView_InsertColumn(m_hSpotList, 4, &lvc);
    lvc.cx = 120; lvc.pszText = const_cast<LPWSTR>(L"Phát Tán"); ListView_InsertColumn(m_hSpotList, 5, &lvc);
    lvc.cx = 125; lvc.pszText = const_cast<LPWSTR>(L"ID Điểm"); ListView_InsertColumn(m_hSpotList, 6, &lvc);

    // Inputs for Tab 2
    m_hLblSpotName = CreateWindowExW(0, L"STATIC", L"Tên:", WS_CHILD, 36, 605, 35, 24, hWnd, nullptr, m_hInstance, nullptr);
    SendMessage(m_hLblSpotName, WM_SETFONT, reinterpret_cast<WPARAM>(m_hFontRegular), TRUE);

    m_hEditSpotName = CreateWindowExW(WS_EX_CLIENTEDGE, L"EDIT", L"Plaza - Bờ Hồ", WS_CHILD | ES_AUTOHSCROLL,
        75, 601, 135, 28, hWnd, reinterpret_cast<HMENU>(IDC_EDIT_SPOT_NAME), m_hInstance, nullptr);
    SendMessage(m_hEditSpotName, WM_SETFONT, reinterpret_cast<WPARAM>(m_hFontRegular), TRUE);

    m_hLblMap = CreateWindowExW(0, L"STATIC", L"Map:", WS_CHILD, 220, 605, 35, 24, hWnd, nullptr, m_hInstance, nullptr);
    SendMessage(m_hLblMap, WM_SETFONT, reinterpret_cast<WPARAM>(m_hFontRegular), TRUE);

    m_hComboMap = CreateWindowExW(0, L"COMBOBOX", L"", WS_CHILD | CBS_DROPDOWNLIST | WS_VSCROLL,
        260, 601, 115, 150, hWnd, reinterpret_cast<HMENU>(IDC_COMBO_MAP), m_hInstance, nullptr);
    SendMessage(m_hComboMap, WM_SETFONT, reinterpret_cast<WPARAM>(m_hFontRegular), TRUE);
    SendMessage(m_hComboMap, CB_ADDSTRING, 0, reinterpret_cast<LPARAM>(L"Plaza (1)"));
    SendMessage(m_hComboMap, CB_ADDSTRING, 0, reinterpret_cast<LPARAM>(L"Downtown (2)"));
    SendMessage(m_hComboMap, CB_ADDSTRING, 0, reinterpret_cast<LPARAM>(L"Camping (3)"));
    SendMessage(m_hComboMap, CB_ADDSTRING, 0, reinterpret_cast<LPARAM>(L"Resort (4)"));
    SendMessage(m_hComboMap, CB_ADDSTRING, 0, reinterpret_cast<LPARAM>(L"Home (10)"));
    SendMessage(m_hComboMap, CB_SETCURSEL, 0, 0);

    m_hLblX = CreateWindowExW(0, L"STATIC", L"X:", WS_CHILD, 385, 605, 18, 24, hWnd, nullptr, m_hInstance, nullptr);
    SendMessage(m_hLblX, WM_SETFONT, reinterpret_cast<WPARAM>(m_hFontRegular), TRUE);
    m_hEditX = CreateWindowExW(WS_EX_CLIENTEDGE, L"EDIT", L"0.0", WS_CHILD | ES_AUTOHSCROLL,
        405, 601, 55, 28, hWnd, reinterpret_cast<HMENU>(IDC_EDIT_SPOT_X), m_hInstance, nullptr);
    SendMessage(m_hEditX, WM_SETFONT, reinterpret_cast<WPARAM>(m_hFontMono), TRUE);

    m_hLblY = CreateWindowExW(0, L"STATIC", L"Y:", WS_CHILD, 468, 605, 18, 24, hWnd, nullptr, m_hInstance, nullptr);
    SendMessage(m_hLblY, WM_SETFONT, reinterpret_cast<WPARAM>(m_hFontRegular), TRUE);
    m_hEditY = CreateWindowExW(WS_EX_CLIENTEDGE, L"EDIT", L"0.0", WS_CHILD | ES_AUTOHSCROLL,
        488, 601, 55, 28, hWnd, reinterpret_cast<HMENU>(IDC_EDIT_SPOT_Y), m_hInstance, nullptr);
    SendMessage(m_hEditY, WM_SETFONT, reinterpret_cast<WPARAM>(m_hFontMono), TRUE);

    m_hLblZ = CreateWindowExW(0, L"STATIC", L"Z:", WS_CHILD, 551, 605, 18, 24, hWnd, nullptr, m_hInstance, nullptr);
    SendMessage(m_hLblZ, WM_SETFONT, reinterpret_cast<WPARAM>(m_hFontRegular), TRUE);
    m_hEditZ = CreateWindowExW(WS_EX_CLIENTEDGE, L"EDIT", L"0.0", WS_CHILD | ES_AUTOHSCROLL,
        571, 601, 55, 28, hWnd, reinterpret_cast<HMENU>(IDC_EDIT_SPOT_Z), m_hInstance, nullptr);
    SendMessage(m_hEditZ, WM_SETFONT, reinterpret_cast<WPARAM>(m_hFontMono), TRUE);

    m_hBtnAddSpot = CreateWindowExW(0, L"BUTTON", L"+ Thêm / Lưu", WS_CHILD | BS_OWNERDRAW,
        636, 599, 130, 32, hWnd, reinterpret_cast<HMENU>(IDC_BTN_SPOT_ADD), m_hInstance, nullptr);

    m_hBtnToggleSpot = CreateWindowExW(0, L"BUTTON", L"⚡ Bật / Tắt", WS_CHILD | BS_OWNERDRAW,
        776, 599, 85, 32, hWnd, reinterpret_cast<HMENU>(IDC_BTN_SPOT_TOGGLE), m_hInstance, nullptr);

    m_hBtnDeleteSpot = CreateWindowExW(0, L"BUTTON", L"🗑 Xóa Điểm", WS_CHILD | BS_OWNERDRAW,
        871, 599, 80, 32, hWnd, reinterpret_cast<HMENU>(IDC_BTN_SPOT_DELETE), m_hInstance, nullptr);

    m_hBtnResetSpots = CreateWindowExW(0, L"BUTTON", L"↻ Mặc Định", WS_CHILD | BS_OWNERDRAW,
        961, 599, 80, 32, hWnd, reinterpret_cast<HMENU>(IDC_BTN_SPOT_RESET), m_hInstance, nullptr);

    // ==========================================
    // TAB 3 CONTROLS: LOGS
    // ==========================================
    m_hEditLogs = CreateWindowExW(WS_EX_CLIENTEDGE, L"EDIT",
        L"[SYSTEM] Master Server Control Center v3.0 Started.\r\n"
        L"[HTTP] Listening asynchronously on http://0.0.0.0:28445\r\n"
        L"[DB] SQLite WAL Initialized. Ready for Client connections.\r\n"
        L"[CACHE] Thin-Server UI Schema & Master Teleport Spots Active.\r\n",
        WS_CHILD | ES_MULTILINE | ES_AUTOVSCROLL | ES_READONLY | WS_VSCROLL,
        34, 222, 1002, 410, hWnd, reinterpret_cast<HMENU>(IDC_EDIT_LOGS), m_hInstance, nullptr);
    SendMessage(m_hEditLogs, WM_SETFONT, reinterpret_cast<WPARAM>(m_hFontMono), TRUE);

    // Refresh Data
    RefreshKeyList();
    RefreshSpotList();
    OnTabChanged(0);
}

void ServerPanelWindow::RefreshKeyList() {
    ListView_DeleteAllItems(m_hKeyList);
    auto keys = database::DatabaseManager::Instance().GetAllKeys();
    m_cachedKeyCount = keys.size();

    int idx = 0;
    for (const auto& k : keys) {
        LVITEMW item{};
        item.mask = LVIF_TEXT;
        item.iItem = idx;
        std::wstring wKey(k.licenseKey.begin(), k.licenseKey.end());
        item.pszText = const_cast<LPWSTR>(wKey.c_str());
        ListView_InsertItem(m_hKeyList, &item);

        std::wstring wHWID = k.boundHWID.empty() ? L"[Chưa Gắn - Sẵn Sàng]" : std::wstring(k.boundHWID.begin(), k.boundHWID.end());
        ListView_SetItemText(m_hKeyList, idx, 1, const_cast<LPWSTR>(wHWID.c_str()));

        // Format expiration
        time_t expSec = static_cast<time_t>(k.expireTimestampMs / 1000);
        tm tmBuf{};
        localtime_s(&tmBuf, &expSec);
        wchar_t dateStr[64]{0};
        wcsftime(dateStr, 64, L"%Y-%m-%d %H:%M", &tmBuf);
        ListView_SetItemText(m_hKeyList, idx, 2, dateStr);

        ListView_SetItemText(m_hKeyList, idx, 3, const_cast<LPWSTR>(L"VIP TRỌN ĐỜI"));
        ListView_SetItemText(m_hKeyList, idx, 4, const_cast<LPWSTR>(k.isActive ? L"HOẠT ĐỘNG" : L"BỊ KHÓA"));
        idx++;
    }

    if (m_hWnd) InvalidateRect(m_hWnd, nullptr, FALSE);
}

void ServerPanelWindow::RefreshSpotList() {
    ListView_DeleteAllItems(m_hSpotList);
    auto spots = services::MasterTeleportService::Instance().GetAllSpots();
    m_cachedSpotCount = spots.size();

    int idx = 0;
    for (const auto& s : spots) {
        LVITEMW item{};
        item.mask = LVIF_TEXT;
        item.iItem = idx;
        std::wstring wName(s.name.begin(), s.name.end());
        item.pszText = const_cast<LPWSTR>(wName.c_str());
        ListView_InsertItem(m_hSpotList, &item);

        std::wstring wMap(s.mapName.begin(), s.mapName.end());
        ListView_SetItemText(m_hSpotList, idx, 1, const_cast<LPWSTR>(wMap.c_str()));

        wchar_t coord[32];
        swprintf_s(coord, L"%.2f", s.position.x);
        ListView_SetItemText(m_hSpotList, idx, 2, coord);
        swprintf_s(coord, L"%.2f", s.position.y);
        ListView_SetItemText(m_hSpotList, idx, 3, coord);
        swprintf_s(coord, L"%.2f", s.position.z);
        ListView_SetItemText(m_hSpotList, idx, 4, coord);

        ListView_SetItemText(m_hSpotList, idx, 5, const_cast<LPWSTR>(s.enabled ? L"ĐANG PHÁT" : L"TẮT"));
        std::wstring wId(s.id.begin(), s.id.end());
        ListView_SetItemText(m_hSpotList, idx, 6, const_cast<LPWSTR>(wId.c_str()));
        idx++;
    }

    if (m_hWnd) InvalidateRect(m_hWnd, nullptr, FALSE);
}

void ServerPanelWindow::OnTabChanged(int newTab) {
    m_currentTab = newTab;

    int showTab1 = (newTab == 0) ? SW_SHOW : SW_HIDE;
    int showTab2 = (newTab == 1) ? SW_SHOW : SW_HIDE;
    int showTab3 = (newTab == 2) ? SW_SHOW : SW_HIDE;

    ShowWindow(m_hKeyList, showTab1);
    ShowWindow(m_hLblNewKey, showTab1);
    ShowWindow(m_hEditNewKey, showTab1);
    ShowWindow(m_hLblDays, showTab1);
    ShowWindow(m_hEditDays, showTab1);
    ShowWindow(m_hBtnCreateKey, showTab1);
    ShowWindow(m_hBtnResetHWID, showTab1);
    ShowWindow(m_hBtnDeleteKey, showTab1);
    ShowWindow(m_hBtnRefreshKeys, showTab1);

    ShowWindow(m_hSpotList, showTab2);
    ShowWindow(m_hLblSpotName, showTab2);
    ShowWindow(m_hEditSpotName, showTab2);
    ShowWindow(m_hLblMap, showTab2);
    ShowWindow(m_hComboMap, showTab2);
    ShowWindow(m_hLblX, showTab2);
    ShowWindow(m_hEditX, showTab2);
    ShowWindow(m_hLblY, showTab2);
    ShowWindow(m_hEditY, showTab2);
    ShowWindow(m_hLblZ, showTab2);
    ShowWindow(m_hEditZ, showTab2);
    ShowWindow(m_hBtnAddSpot, showTab2);
    ShowWindow(m_hBtnToggleSpot, showTab2);
    ShowWindow(m_hBtnDeleteSpot, showTab2);
    ShowWindow(m_hBtnResetSpots, showTab2);

    ShowWindow(m_hEditLogs, showTab3);

    // Invalidate tab switcher buttons and container
    InvalidateRect(m_hBtnTab0, nullptr, TRUE);
    InvalidateRect(m_hBtnTab1, nullptr, TRUE);
    InvalidateRect(m_hBtnTab2, nullptr, TRUE);
    InvalidateRect(m_hWnd, nullptr, FALSE);
}

void ServerPanelWindow::AppendLog(const std::string& logLine) {
    if (!m_hEditLogs) return;
    std::wstring wLog(logLine.begin(), logLine.end());
    wLog += L"\r\n";
    int len = GetWindowTextLengthW(m_hEditLogs);
    SendMessageW(m_hEditLogs, EM_SETSEL, len, len);
    SendMessageW(m_hEditLogs, EM_REPLACESEL, FALSE, reinterpret_cast<LPARAM>(wLog.c_str()));
}

void ServerPanelWindow::DrawStatCard(HDC hdc, const RECT& rc, const std::wstring& title,
                                      const std::wstring& value, const std::wstring& subtitle,
                                      COLORREF accentColor) {
    // Background & border
    theme::DrawRoundedRect(hdc, rc, theme::Color::Card, theme::Color::BorderSubtle, theme::Radius::Medium);

    // Left accent vertical pill indicator
    RECT rcPill{rc.left + 12, rc.top + 16, rc.left + 16, rc.bottom - 16};
    theme::DrawRoundedRect(hdc, rcPill, accentColor, accentColor, 2);

    SetBkMode(hdc, TRANSPARENT);

    // Title (Muted)
    HGDIOBJ oldF = SelectObject(hdc, m_hFontSmall);
    SetTextColor(hdc, theme::Color::TextSecondary);
    TextOutW(hdc, rc.left + 26, rc.top + 14, title.c_str(), static_cast<int>(title.size()));

    // Value (Bold Accent)
    SelectObject(hdc, m_hFontBold);
    SetTextColor(hdc, accentColor);
    TextOutW(hdc, rc.left + 26, rc.top + 33, value.c_str(), static_cast<int>(value.size()));

    // Subtitle (Dim)
    SelectObject(hdc, m_hFontSmall);
    SetTextColor(hdc, theme::Color::TextMuted);
    TextOutW(hdc, rc.left + 26, rc.top + 54, subtitle.c_str(), static_cast<int>(subtitle.size()));

    SelectObject(hdc, oldF);
}

LRESULT ServerPanelWindow::HandleMessage(HWND hWnd, UINT msg, WPARAM wParam, LPARAM lParam) {
    switch (msg) {
    case WM_ERASEBKGND:
        return 1; // Prevent all flicker

    case WM_PAINT: {
        PAINTSTRUCT ps;
        HDC hdc = BeginPaint(hWnd, &ps);

        RECT rcClient;
        GetClientRect(hWnd, &rcClient);
        int w = rcClient.right - rcClient.left;
        int h = rcClient.bottom - rcClient.top;

        HDC memDC = CreateCompatibleDC(hdc);
        HBITMAP memBmp = CreateCompatibleBitmap(hdc, w, h);
        HGDIOBJ oldBmp = SelectObject(memDC, memBmp);

        // 1. Fill main background (#0B0F17)
        FillRect(memDC, &rcClient, m_hBgBrush);

        // 2. Top Header area (y = 0 to 60)
        SetBkMode(memDC, TRANSPARENT);
        HGDIOBJ oldFont = SelectObject(memDC, m_hFontTitle);
        SetTextColor(memDC, theme::Color::VipGold);
        TextOutW(memDC, 20, 16, L"✦ DTA PLAYTOGETHER", 18);

        SelectObject(memDC, m_hFontRegular);
        SetTextColor(memDC, theme::Color::TextSecondary);
        TextOutW(memDC, 245, 20, L"MASTER SERVER CONTROL CENTER v3.0", 33);

        // Header Right: Status badge
        RECT rcBadge{w - 240, 14, w - 20, 48};
        theme::DrawStatusBadge(memDC, rcBadge, L"SERVER ONLINE : 28445",
                               theme::Color::Success, theme::Color::Card, theme::Color::BorderSubtle, m_hFontBold);

        // 3. Header 4 Stat Cards (y = 66 to 150)
        int cardY = 66;
        int cardH = 82;
        int marginX = 20;
        int totalW = w - 40;
        int gap = 12;
        int cardW = (totalW - 3 * gap) / 4;

        // Card 1: Server Status
        RECT rcCard1{marginX, cardY, marginX + cardW, cardY + cardH};
        DrawStatCard(memDC, rcCard1, L"TRẠNG THÁI SERVER", L"ONLINE (0.0.0.0)", L"Cổng 28445 • SQLite WAL", theme::Color::Success);

        // Card 2: License Keys
        RECT rcCard2{marginX + cardW + gap, cardY, marginX + 2 * cardW + gap, cardY + cardH};
        std::wstring keyText = std::to_wstring(m_cachedKeyCount) + L" Keys Đã Tạo";
        DrawStatCard(memDC, rcCard2, L"QUẢN LÝ BẢN QUYỀN", keyText, L"Bảo vệ HWID tự động", theme::Color::VipGold);

        // Card 3: Master Teleport
        RECT rcCard3{marginX + 2 * (cardW + gap), cardY, marginX + 3 * cardW + 2 * gap, cardY + cardH};
        std::wstring spotText = std::to_wstring(m_cachedSpotCount) + L" Điểm Master";
        DrawStatCard(memDC, rcCard3, L"MASTER TELEPORT", spotText, L"Phát tán toàn Client", theme::Color::CyanNeon);

        // Card 4: REST API Engine
        RECT rcCard4{marginX + 3 * (cardW + gap), cardY, marginX + 4 * cardW + 3 * gap, cardY + cardH};
        DrawStatCard(memDC, rcCard4, L"REST API ENGINE", L"< 1ms Response", L"Non-blocking C++ Core", theme::Color::Accent);

        // 4. Content Area Card Background (y = 212 to 648)
        RECT rcContent{20, 212, w - 20, 648};
        theme::DrawRoundedRect(memDC, rcContent, theme::Color::Card, theme::Color::BorderSubtle, theme::Radius::Medium);

        // 5. Footer (y = 654 to 680)
        SelectObject(memDC, m_hFontSmall);
        SetTextColor(memDC, theme::Color::TextMuted);
        TextOutW(memDC, 24, 658, L"DTA Native Core • Server-Driven UI Engine • High Concurrency SQLite WAL • Zero-Disk Log Safe", 92);

        SetTextColor(memDC, theme::Color::Accent);
        TextOutW(memDC, w - 190, 658, L"60 FPS Double Buffered", 22);

        SelectObject(memDC, oldFont);

        // Blit to screen
        BitBlt(hdc, 0, 0, w, h, memDC, 0, 0, SRCCOPY);
        SelectObject(memDC, oldBmp);
        DeleteObject(memBmp);
        DeleteDC(memDC);

        EndPaint(hWnd, &ps);
        return 0;
    }

    case WM_DRAWITEM: {
        auto dis = reinterpret_cast<LPDRAWITEMSTRUCT>(lParam);
        HDC hdc = dis->hDC;
        RECT rc = dis->rcItem;
        bool isSelected = (dis->itemState & ODS_SELECTED) != 0;

        SetBkMode(hdc, TRANSPARENT);

        // 1. Segmented Tab Switcher Buttons
        if (dis->CtlID == IDC_TAB_BTN_0 || dis->CtlID == IDC_TAB_BTN_1 || dis->CtlID == IDC_TAB_BTN_2) {
            int tabIndex = (dis->CtlID == IDC_TAB_BTN_0) ? 0 : ((dis->CtlID == IDC_TAB_BTN_1) ? 1 : 2);
            bool isActive = (m_currentTab == tabIndex);

            COLORREF bg = isActive ? theme::Color::Accent : (isSelected ? theme::Color::CardHover : theme::Color::Card);
            COLORREF border = isActive ? theme::Color::AccentHover : theme::Color::BorderSubtle;
            COLORREF textCol = isActive ? RGB(255, 255, 255) : (isSelected ? theme::Color::TextPrimary : theme::Color::TextSecondary);

            wchar_t btnText[64]{0};
            GetWindowTextW(dis->hwndItem, btnText, 64);
            theme::DrawModernButton(hdc, rc, btnText, bg, border, textCol, m_hFontBold, theme::Radius::Normal);
            return TRUE;
        }

        // 2. Action Buttons
        switch (dis->CtlID) {
        case IDC_BTN_KEY_CREATE: {
            COLORREF bg = isSelected ? theme::Color::AccentHover : theme::Color::Accent;
            theme::DrawModernButton(hdc, rc, L"+ Tạo Key Mới", bg, theme::Color::AccentHover, RGB(255, 255, 255), m_hFontBold, theme::Radius::Normal);
            return TRUE;
        }
        case IDC_BTN_KEY_RESETHWID: {
            COLORREF bg = isSelected ? theme::Color::SuccessHover : theme::Color::Success;
            theme::DrawModernButton(hdc, rc, L"🔄 Reset HWID", bg, theme::Color::SuccessHover, RGB(255, 255, 255), m_hFontBold, theme::Radius::Normal);
            return TRUE;
        }
        case IDC_BTN_KEY_DELETE: {
            COLORREF bg = isSelected ? theme::Color::DangerHover : theme::Color::Card;
            COLORREF textCol = isSelected ? RGB(255, 255, 255) : theme::Color::Danger;
            theme::DrawModernButton(hdc, rc, L"🗑 Xóa Key", bg, theme::Color::Danger, textCol, m_hFontBold, theme::Radius::Normal);
            return TRUE;
        }
        case IDC_BTN_KEY_REFRESH: {
            COLORREF bg = isSelected ? theme::Color::CardHover : theme::Color::CardSecondary;
            theme::DrawModernButton(hdc, rc, L"↻ Làm Mới", bg, theme::Color::BorderLight, theme::Color::AccentHover, m_hFontBold, theme::Radius::Normal);
            return TRUE;
        }
        case IDC_BTN_SPOT_ADD: {
            COLORREF bg = isSelected ? theme::Color::AccentHover : theme::Color::Accent;
            theme::DrawModernButton(hdc, rc, L"+ Thêm / Lưu", bg, theme::Color::AccentHover, RGB(255, 255, 255), m_hFontBold, theme::Radius::Normal);
            return TRUE;
        }
        case IDC_BTN_SPOT_TOGGLE: {
            COLORREF bg = isSelected ? theme::Color::SuccessHover : theme::Color::Success;
            theme::DrawModernButton(hdc, rc, L"⚡ Bật / Tắt", bg, theme::Color::SuccessHover, RGB(255, 255, 255), m_hFontBold, theme::Radius::Normal);
            return TRUE;
        }
        case IDC_BTN_SPOT_DELETE: {
            COLORREF bg = isSelected ? theme::Color::DangerHover : theme::Color::Card;
            COLORREF textCol = isSelected ? RGB(255, 255, 255) : theme::Color::Danger;
            theme::DrawModernButton(hdc, rc, L"🗑 Xóa Điểm", bg, theme::Color::Danger, textCol, m_hFontBold, theme::Radius::Normal);
            return TRUE;
        }
        case IDC_BTN_SPOT_RESET: {
            COLORREF bg = isSelected ? theme::Color::CardHover : theme::Color::CardSecondary;
            theme::DrawModernButton(hdc, rc, L"↻ Mặc Định", bg, theme::Color::BorderLight, theme::Color::TextSecondary, m_hFontBold, theme::Radius::Normal);
            return TRUE;
        }
        }
        break;
    }

    case WM_COMMAND: {
        int id = LOWORD(wParam);
        if (id == IDC_TAB_BTN_0) {
            OnTabChanged(0);
        } else if (id == IDC_TAB_BTN_1) {
            OnTabChanged(1);
        } else if (id == IDC_TAB_BTN_2) {
            OnTabChanged(2);
        } else if (id == IDC_BTN_KEY_CREATE) {
            wchar_t keyBuf[128]{0};
            wchar_t daysBuf[32]{0};
            GetWindowTextW(m_hEditNewKey, keyBuf, 128);
            GetWindowTextW(m_hEditDays, daysBuf, 32);
            int days = _wtoi(daysBuf);
            if (days <= 0) days = 30;

            char mbKey[128]{0};
            WideCharToMultiByte(CP_UTF8, 0, keyBuf, -1, mbKey, 128, nullptr, nullptr);
            if (database::DatabaseManager::Instance().CreateKey(mbKey, days, 1)) {
                RefreshKeyList();
                AppendLog(std::string("[KEY] Created new license key: ") + mbKey);
                MessageBoxW(hWnd, L"Đã tạo Key thành công!", L"Thông Báo", MB_OK | MB_ICONINFORMATION);
            }
        } else if (id == IDC_BTN_KEY_REFRESH) {
            RefreshKeyList();
        } else if (id == IDC_BTN_KEY_RESETHWID) {
            int sel = ListView_GetNextItem(m_hKeyList, -1, LVNI_SELECTED);
            if (sel >= 0) {
                wchar_t kStr[128]{0};
                ListView_GetItemText(m_hKeyList, sel, 0, kStr, 128);
                char mbKey[128]{0};
                WideCharToMultiByte(CP_UTF8, 0, kStr, -1, mbKey, 128, nullptr, nullptr);
                database::DatabaseManager::Instance().ResetHWID(mbKey);
                RefreshKeyList();
                AppendLog(std::string("[KEY] Reset bound HWID for key: ") + mbKey);
                MessageBoxW(hWnd, L"Đã Reset HWID thành công! Key có thể bind trên máy mới.", L"Thông Báo", MB_OK | MB_ICONINFORMATION);
            }
        } else if (id == IDC_BTN_KEY_DELETE) {
            int sel = ListView_GetNextItem(m_hKeyList, -1, LVNI_SELECTED);
            if (sel >= 0) {
                wchar_t kStr[128]{0};
                ListView_GetItemText(m_hKeyList, sel, 0, kStr, 128);
                char mbKey[128]{0};
                WideCharToMultiByte(CP_UTF8, 0, kStr, -1, mbKey, 128, nullptr, nullptr);
                database::DatabaseManager::Instance().DeleteKey(mbKey);
                RefreshKeyList();
                AppendLog(std::string("[KEY] Deleted key: ") + mbKey);
            }
        } else if (id == IDC_BTN_SPOT_ADD) {
            wchar_t nameBuf[128]{0}, xBuf[32]{0}, yBuf[32]{0}, zBuf[32]{0};
            GetWindowTextW(m_hEditSpotName, nameBuf, 128);
            GetWindowTextW(m_hEditX, xBuf, 32);
            GetWindowTextW(m_hEditY, yBuf, 32);
            GetWindowTextW(m_hEditZ, zBuf, 32);
            int mapSel = static_cast<int>(SendMessage(m_hComboMap, CB_GETCURSEL, 0, 0));

            uint32_t mapId = 1;
            std::string mapName = "Plaza";
            if (mapSel == 1) { mapId = 2; mapName = "Downtown"; }
            else if (mapSel == 2) { mapId = 3; mapName = "Camping"; }
            else if (mapSel == 3) { mapId = 4; mapName = "Resort"; }
            else if (mapSel == 4) { mapId = 10; mapName = "Home"; }

            char mbName[128]{0};
            WideCharToMultiByte(CP_UTF8, 0, nameBuf, -1, mbName, 128, nullptr, nullptr);

            services::MasterTeleportSpot spot;
            spot.id = "spot_" + std::to_string(GetTickCount64());
            spot.name = mbName;
            spot.mapId = mapId;
            spot.mapName = mapName;
            spot.position.x = static_cast<float>(_wtof(xBuf));
            spot.position.y = static_cast<float>(_wtof(yBuf));
            spot.position.z = static_cast<float>(_wtof(zBuf));
            spot.enabled = true;

            services::MasterTeleportService::Instance().AddOrUpdateSpot(spot);
            RefreshSpotList();
            AppendLog(std::string("[TELE] Broadcasted new Master Spot: ") + mbName);
            MessageBoxW(hWnd, L"Đã lưu và phát tán Điểm Tele Master xuống toàn bộ Client!", L"Thành Công", MB_OK | MB_ICONINFORMATION);
        } else if (id == IDC_BTN_SPOT_TOGGLE) {
            int sel = ListView_GetNextItem(m_hSpotList, -1, LVNI_SELECTED);
            if (sel >= 0) {
                wchar_t idStr[64]{0};
                ListView_GetItemText(m_hSpotList, sel, 6, idStr, 64);
                char mbId[64]{0};
                WideCharToMultiByte(CP_UTF8, 0, idStr, -1, mbId, 64, nullptr, nullptr);
                services::MasterTeleportService::Instance().ToggleSpot(mbId);
                RefreshSpotList();
            }
        } else if (id == IDC_BTN_SPOT_DELETE) {
            int sel = ListView_GetNextItem(m_hSpotList, -1, LVNI_SELECTED);
            if (sel >= 0) {
                wchar_t idStr[64]{0};
                ListView_GetItemText(m_hSpotList, sel, 6, idStr, 64);
                char mbId[64]{0};
                WideCharToMultiByte(CP_UTF8, 0, idStr, -1, mbId, 64, nullptr, nullptr);
                services::MasterTeleportService::Instance().DeleteSpot(mbId);
                RefreshSpotList();
            }
        } else if (id == IDC_BTN_SPOT_RESET) {
            services::MasterTeleportService::Instance().InitDefaults();
            RefreshSpotList();
            MessageBoxW(hWnd, L"Đã phục hồi danh sách Điểm Master mặc định!", L"Thông Báo", MB_OK | MB_ICONINFORMATION);
        }
        break;
    }

    case WM_CTLCOLORSTATIC: {
        HDC hdcStatic = reinterpret_cast<HDC>(wParam);
        HWND hwndStatic = reinterpret_cast<HWND>(lParam);
        if (hwndStatic == m_hEditLogs) {
            SetTextColor(hdcStatic, theme::Color::TextPrimary);
            SetBkColor(hdcStatic, theme::Color::CardHover);
            return reinterpret_cast<INT_PTR>(m_hCardBrush);
        }
        SetTextColor(hdcStatic, theme::Color::TextSecondary);
        SetBkColor(hdcStatic, theme::Color::Card);
        return reinterpret_cast<INT_PTR>(m_hCardBrush);
    }

    case WM_CTLCOLOREDIT: {
        HDC hdcEdit = reinterpret_cast<HDC>(wParam);
        SetTextColor(hdcEdit, theme::Color::TextPrimary);
        SetBkColor(hdcEdit, theme::Color::InputBg);
        return reinterpret_cast<INT_PTR>(m_hInputBrush);
    }

    case WM_CTLCOLORLISTBOX: {
        HDC hdcList = reinterpret_cast<HDC>(wParam);
        SetTextColor(hdcList, theme::Color::TextPrimary);
        SetBkColor(hdcList, theme::Color::InputBg);
        return reinterpret_cast<INT_PTR>(m_hInputBrush);
    }

    case WM_DESTROY:
        PostQuitMessage(0);
        return 0;
    }

    return DefWindowProcW(hWnd, msg, wParam, lParam);
}

void ServerPanelWindow::RunMessageLoop() {
    MSG msg;
    while (GetMessageW(&msg, nullptr, 0, 0)) {
        TranslateMessage(&msg);
        DispatchMessageW(&msg);
    }
}

} // namespace dta::server::ui
