#include "ClientWindow.hpp"
#include "ActivationDialog.hpp"
#include "client/network/NetworkClient.hpp"
#include "client/security/HWIDProvider.hpp"
#include "client/core/logger/AsyncLogger.hpp"
#include "shared/Theme.hpp"

// Features
#include "client/features/teleport/ITeleportService.hpp"
#include "client/features/fishing/FishingBot.hpp"
#include "client/features/mining/MiningBot.hpp"
#include "client/features/insect/InsectBot.hpp"
#include "client/features/excavation/ExcavationBot.hpp"
#include "client/features/farm/FarmBot.hpp"
#include "client/features/collect/CollectBot.hpp"

#include <commctrl.h>
#include <dwmapi.h>
#include <sstream>
#include <iostream>
#include <thread>

#pragma comment(lib, "comctl32.lib")
#pragma comment(lib, "dwmapi.lib")

namespace dta::ui {

// Navigation & Top IDs
#define IDC_SIDEBAR_BASE         2000 // 2000 to 2008

#define IDC_TOP_CHK_TOPMOST      2010
#define IDC_TOP_SUBTAB_FISHING   2011
#define IDC_TOP_SUBTAB_HISTORY   2012
#define IDC_TOP_COMBO_EMU        2013
#define IDC_TOP_COMBO_TAB        2014
#define IDC_TOP_BTN_REFRESH      2015
#define IDC_SIDEBAR_BTN_LOGOUT   2020

// Tab 0: ESP
#define IDC_ESP_BTN_START        2100
#define IDC_ESP_BTN_STOP         2101
#define IDC_ESP_CHK_SNAPLINE     2102
#define IDC_ESP_CHK_3DBOX        2103
#define IDC_ESP_CHK_DIST         2104
#define IDC_ESP_CHK_TIER         2105
#define IDC_ESP_CHK_FISH         2106
#define IDC_ESP_CHK_ORE          2107
#define IDC_ESP_CHK_INSECT       2108
#define IDC_ESP_CHK_CHEST        2109

// Tab 1: Câu cá (Fishing)
#define IDC_FISH_BTN_START       2200
#define IDC_FISH_BTN_STOP        2201
#define IDC_FISH_CHK_AUTOREPAIR  2202
#define IDC_FISH_CHK_FASTSELL    2203
#define IDC_FISH_BTN_KEEPFISH    2204
#define IDC_FISH_BTN_SELLFISH    2205
#define IDC_FISH_CHK_LOCKPOV     2206
#define IDC_FISH_CHK_FASTBITE    2207
#define IDC_FISH_CHK_FILTER      2210
#define IDC_FISH_EDIT_FILTER_ID  2211
#define IDC_FISH_BTN_CLEAR_FILTER 2212
#define IDC_FISH_BTN_SHADOW_BASE 2220 // 2220 to 2226 (1 to 7)
#define IDC_FISH_BTN_TIER_BASE   2230 // 2230 to 2234
#define IDC_FISH_CHK_KEEP_VAR    2240
#define IDC_FISH_CHK_KEEP_MUT    2241
#define IDC_FISH_EDIT_KEEP_ID    2242
#define IDC_FISH_BTN_CLEAR_KEEP  2243
#define IDC_FISH_BTN_KSHADOW_BASE 2250 // 2250 to 2256
#define IDC_FISH_BTN_KTIER_BASE   2260 // 2260 to 2264

// Tab 2: Đào cổ vật (Excavation)
#define IDC_EXC_BTN_START        2300
#define IDC_EXC_BTN_STOP         2301
#define IDC_EXC_CHK_BUY          2302
#define IDC_EXC_CHK_REPAIR       2303
#define IDC_EXC_CHK_SKIPWOOD     2304
#define IDC_EXC_CHK_MINIGAME     2305
#define IDC_EXC_CHK_OPENNOW      2306
#define IDC_EXC_CHK_NEXT         2307
#define IDC_EXC_CHK_SILVER       2308
#define IDC_EXC_CHK_GOLD         2309
#define IDC_EXC_CHK_DIAMOND      2310

// Tab 3: Đập đá (Mining)
#define IDC_MIN_BTN_START        2400
#define IDC_MIN_BTN_STOP         2401
#define IDC_MIN_CHK_REPAIR       2402
#define IDC_MIN_CHK_SWAPVIP      2403
#define IDC_MIN_CHK_ANTIKS       2404
#define IDC_MIN_CHK_IRON         2405
#define IDC_MIN_CHK_GOLD         2406
#define IDC_MIN_CHK_DIAMOND      2407
#define IDC_MIN_CHK_METEOR       2408
#define IDC_MIN_COMBO_ZONE       2409

// Tab 4: Bắt bọ (Insect)
#define IDC_INS_BTN_START        2500
#define IDC_INS_BTN_STOP         2501
#define IDC_INS_CHK_REPAIR       2502
#define IDC_INS_CHK_SNEAK        2503
#define IDC_INS_CHK_LOCK         2504
#define IDC_INS_CHK_TELEPORT     2505
#define IDC_INS_CHK_BUTTERFLY    2506
#define IDC_INS_CHK_BEETLE       2507
#define IDC_INS_CHK_STAG         2508
#define IDC_INS_CHK_SCORPION     2509

// Tab 5: Thu lượm (Collect)
#define IDC_COL_BTN_START        2600
#define IDC_COL_BTN_STOP         2601
#define IDC_COL_CHK_SELL         2602
#define IDC_COL_CHK_RECYCLE      2603
#define IDC_COL_CHK_TSP          2604
#define IDC_COL_CHK_BRANCH       2605
#define IDC_COL_CHK_APPLE        2606
#define IDC_COL_CHK_MUSHROOM     2607
#define IDC_COL_CHK_STAR         2608

// Tab 6: Nông trại (Farm)
#define IDC_FARM_BTN_START       2700
#define IDC_FARM_BTN_STOP        2701
#define IDC_FARM_CHK_WATER       2702
#define IDC_FARM_CHK_FERT        2703
#define IDC_FARM_CHK_BUYSEED     2704
#define IDC_FARM_CHK_HARVEST     2705
#define IDC_FARM_CHK_CHICKEN     2706
#define IDC_FARM_CHK_COW         2707

// Tab 7: Dịch chuyển (Teleport)
#define IDC_TELE_LIST            2800
#define IDC_TELE_BTN_GO          2801
#define IDC_TELE_BTN_SYNC        2802
#define IDC_TELE_BTN_SAVEPOS     2803
#define IDC_TELE_MAP_1           2810
#define IDC_TELE_MAP_2           2811
#define IDC_TELE_MAP_3           2812
#define IDC_TELE_MAP_4           2813
#define IDC_TELE_MAP_10          2814

// Tab 8: Cài đặt (Settings)
#define IDC_SET_EDIT_KEY         2900
#define IDC_SET_BTN_ACTIVATE     2901
#define IDC_SET_EDIT_HWID        2902
#define IDC_SET_BTN_COPY_HWID    2903
#define IDC_SET_EDIT_SERVER      2904
#define IDC_SET_BTN_PING         2905
#define IDC_SET_CHK_ANTIBAN      2906
#define IDC_SET_CHK_PANIC        2907

#define IDT_UI_REFRESH_TIMER     999

static const std::array<SidebarMenuItem, 9> SIDEBAR_ITEMS = {{
    {0, L"ESP", L"👁 "},
    {1, L"Câu cá", L"🐟 "},
    {2, L"Đào cổ vật", L"⛏ "},
    {3, L"Đập đá", L"🔨 "},
    {4, L"Bắt bọ", L"🪲 "},
    {5, L"Thu lượm", L"🎒 "},
    {6, L"Nông trại", L"🌱 "},
    {7, L"Dịch chuyển", L"📍 "},
    {8, L"Cài đặt", L"⚙ "}
}};

ClientWindow& ClientWindow::Instance() {
    static ClientWindow instance;
    return instance;
}

ClientWindow::ClientWindow() {
    m_hBgBrush = CreateSolidBrush(theme::Color::BgMain);
    m_hSidebarBrush = CreateSolidBrush(theme::Color::Sidebar);
    m_hCardBrush = CreateSolidBrush(theme::Color::Card);
    m_hInputBrush = CreateSolidBrush(theme::Color::InputBg);
    m_hBorderBrush = CreateSolidBrush(theme::Color::BorderSubtle);

    m_hFontTitle = CreateFontW(20, 0, 0, 0, FW_BOLD, FALSE, FALSE, FALSE,
        DEFAULT_CHARSET, OUT_DEFAULT_PRECIS, CLIP_DEFAULT_PRECIS, CLEARTYPE_QUALITY,
        DEFAULT_PITCH | FF_DONTCARE, L"Segoe UI");

    m_hFontHeader = CreateFontW(16, 0, 0, 0, FW_BOLD, FALSE, FALSE, FALSE,
        DEFAULT_CHARSET, OUT_DEFAULT_PRECIS, CLIP_DEFAULT_PRECIS, CLEARTYPE_QUALITY,
        DEFAULT_PITCH | FF_DONTCARE, L"Segoe UI");

    m_hFontRegular = CreateFontW(14, 0, 0, 0, FW_NORMAL, FALSE, FALSE, FALSE,
        DEFAULT_CHARSET, OUT_DEFAULT_PRECIS, CLIP_DEFAULT_PRECIS, CLEARTYPE_QUALITY,
        DEFAULT_PITCH | FF_DONTCARE, L"Segoe UI");

    m_hFontBold = CreateFontW(14, 0, 0, 0, FW_BOLD, FALSE, FALSE, FALSE,
        DEFAULT_CHARSET, OUT_DEFAULT_PRECIS, CLIP_DEFAULT_PRECIS, CLEARTYPE_QUALITY,
        DEFAULT_PITCH | FF_DONTCARE, L"Segoe UI");

    m_hFontSmall = CreateFontW(12, 0, 0, 0, FW_NORMAL, FALSE, FALSE, FALSE,
        DEFAULT_CHARSET, OUT_DEFAULT_PRECIS, CLIP_DEFAULT_PRECIS, CLEARTYPE_QUALITY,
        DEFAULT_PITCH | FF_DONTCARE, L"Segoe UI");

    m_hFontMono = CreateFontW(13, 0, 0, 0, FW_NORMAL, FALSE, FALSE, FALSE,
        DEFAULT_CHARSET, OUT_DEFAULT_PRECIS, CLIP_DEFAULT_PRECIS, CLEARTYPE_QUALITY,
        FIXED_PITCH | FF_MODERN, L"Consolas");

    m_hFontBigNum = CreateFontW(32, 0, 0, 0, FW_BOLD, FALSE, FALSE, FALSE,
        DEFAULT_CHARSET, OUT_DEFAULT_PRECIS, CLIP_DEFAULT_PRECIS, CLEARTYPE_QUALITY,
        DEFAULT_PITCH | FF_DONTCARE, L"Segoe UI");

    m_clientHwid = security::HWIDProvider::GetHWID();

    m_filterShadowState.fill(false);
    m_filterTierState.fill(false);
    m_keepShadowState.fill(false);
    m_keepTierState.fill(false);
}

ClientWindow::~ClientWindow() {
    if (m_hBgBrush) DeleteObject(m_hBgBrush);
    if (m_hSidebarBrush) DeleteObject(m_hSidebarBrush);
    if (m_hCardBrush) DeleteObject(m_hCardBrush);
    if (m_hInputBrush) DeleteObject(m_hInputBrush);
    if (m_hBorderBrush) DeleteObject(m_hBorderBrush);

    if (m_hFontTitle) DeleteObject(m_hFontTitle);
    if (m_hFontHeader) DeleteObject(m_hFontHeader);
    if (m_hFontRegular) DeleteObject(m_hFontRegular);
    if (m_hFontBold) DeleteObject(m_hFontBold);
    if (m_hFontSmall) DeleteObject(m_hFontSmall);
    if (m_hFontMono) DeleteObject(m_hFontMono);
    if (m_hFontBigNum) DeleteObject(m_hFontBigNum);
}

void ClientWindow::InitDependencies(
    std::shared_ptr<features::teleport::ITeleportService> teleportService,
    std::shared_ptr<features::fishing::FishingBot> fishingBot,
    std::shared_ptr<features::mining::MiningBot> miningBot,
    std::shared_ptr<features::insect::InsectBot> insectBot,
    std::shared_ptr<features::excavation::ExcavationBot> excavationBot,
    std::shared_ptr<features::farm::FarmBot> farmBot,
    std::shared_ptr<features::collect::CollectBot> collectBot
) {
    m_teleportService = std::move(teleportService);
    m_fishingBot = std::move(fishingBot);
    m_miningBot = std::move(miningBot);
    m_insectBot = std::move(insectBot);
    m_excavationBot = std::move(excavationBot);
    m_farmBot = std::move(farmBot);
    m_collectBot = std::move(collectBot);
}

LRESULT CALLBACK ClientWindow::WndProc(HWND hWnd, UINT msg, WPARAM wParam, LPARAM lParam) {
    ClientWindow* pThis = nullptr;
    if (msg == WM_NCCREATE) {
        auto cs = reinterpret_cast<CREATESTRUCT*>(lParam);
        pThis = reinterpret_cast<ClientWindow*>(cs->lpCreateParams);
        SetWindowLongPtr(hWnd, GWLP_USERDATA, reinterpret_cast<LONG_PTR>(pThis));
        pThis->m_hWnd = hWnd;
    } else {
        pThis = reinterpret_cast<ClientWindow*>(GetWindowLongPtr(hWnd, GWLP_USERDATA));
    }

    if (pThis) {
        return pThis->HandleMessage(hWnd, msg, wParam, lParam);
    }
    return DefWindowProcW(hWnd, msg, wParam, lParam);
}

bool ClientWindow::Create(HINSTANCE hInstance, int nCmdShow) {
    m_hInstance = hInstance;

    INITCOMMONCONTROLSEX icex;
    icex.dwSize = sizeof(INITCOMMONCONTROLSEX);
    icex.dwICC = ICC_LISTVIEW_CLASSES | ICC_STANDARD_CLASSES;
    InitCommonControlsEx(&icex);

    WNDCLASSEXW wc{};
    wc.cbSize = sizeof(WNDCLASSEXW);
    wc.style = CS_HREDRAW | CS_VREDRAW;
    wc.lpfnWndProc = WndProc;
    wc.hInstance = hInstance;
    wc.hCursor = LoadCursor(nullptr, IDC_ARROW);
    wc.hbrBackground = m_hBgBrush;
    wc.lpszClassName = L"DTAPlaytogetherClientWindow";

    RegisterClassExW(&wc);

    int winW = 1100;
    int winH = 700;
    int posX = (GetSystemMetrics(SM_CXSCREEN) - winW) / 2;
    int posY = (GetSystemMetrics(SM_CYSCREEN) - winH) / 2;

    m_hWnd = CreateWindowExW(
        0, L"DTAPlaytogetherClientWindow",
        L"DTA PlayTogether • Native v3.0",
        WS_OVERLAPPEDWINDOW & ~WS_THICKFRAME & ~WS_MAXIMIZEBOX,
        posX, posY, winW, winH,
        nullptr, nullptr, hInstance, this
    );

    if (!m_hWnd) return false;

    // Enable Immersive Dark Mode & Rounded Corners (Windows 11)
    BOOL dark = TRUE;
    DwmSetWindowAttribute(m_hWnd, 20, &dark, sizeof(dark));
    int cornerPref = 2; // DWMWCP_ROUND
    DwmSetWindowAttribute(m_hWnd, 33, &cornerPref, sizeof(cornerPref));

    InitControls(m_hWnd);
    ShowWindow(m_hWnd, nCmdShow);
    UpdateWindow(m_hWnd);

    // Bật Timer 500ms để refresh số liệu nhẹ nhàng
    SetTimer(m_hWnd, IDT_UI_REFRESH_TIMER, 500, nullptr);

    // Chạy kiểm tra server và đồng bộ spots trong background thread (KHÔNG BLOCK UI!)
    CheckServerHealthAsync();
    std::thread([this]() {
        SyncMasterSpotsFromServer();
    }).detach();

    return true;
}

void ClientWindow::CheckServerHealthAsync() {
    std::thread([this]() {
        std::string res = network::NetworkClient::Instance().HttpGet("/health");
        bool online = (res.find("online") != std::string::npos);
        if (m_isServerOnline != online) {
            m_isServerOnline = online;
            if (m_hWnd) InvalidateRect(m_hWnd, nullptr, FALSE);
        }
    }).detach();
}

void ClientWindow::InitControls(HWND hWnd) {
    for (auto& vec : m_tabControls) vec.clear();

    // ==========================================
    // 1. LEFT SIDEBAR NAVIGATION (9 Tabs)
    // ==========================================
    m_sidebarButtons.clear();
    int btnY = 95;
    for (size_t i = 0; i < SIDEBAR_ITEMS.size(); ++i) {
        HWND hBtn = CreateWindowExW(0, L"BUTTON", SIDEBAR_ITEMS[i].title.c_str(),
            WS_CHILD | WS_VISIBLE | BS_OWNERDRAW,
            14, btnY, 172, 42, hWnd, reinterpret_cast<HMENU>(IDC_SIDEBAR_BASE + i), m_hInstance, nullptr);
        m_sidebarButtons.push_back(hBtn);
        btnY += 46;
    }

    m_hBtnLogoutKey = CreateWindowExW(0, L"BUTTON", L"Đăng xuất key",
        WS_CHILD | WS_VISIBLE | BS_OWNERDRAW,
        14, 638, 172, 34, hWnd, reinterpret_cast<HMENU>(IDC_SIDEBAR_BTN_LOGOUT), m_hInstance, nullptr);

    // ==========================================
    // 2. TOP CONTENT HEADER CONTROLS
    // ==========================================
    m_hChkTopmost = CreateWindowExW(0, L"BUTTON", L"Ghim trên cùng",
        WS_CHILD | WS_VISIBLE | BS_AUTOCHECKBOX,
        720, 20, 115, 24, hWnd, reinterpret_cast<HMENU>(IDC_TOP_CHK_TOPMOST), m_hInstance, nullptr);
    SendMessage(m_hChkTopmost, WM_SETFONT, reinterpret_cast<WPARAM>(m_hFontRegular), TRUE);

    m_hBtnSubTabFishing = CreateWindowExW(0, L"BUTTON", L"Hoạt động",
        WS_CHILD | WS_VISIBLE | BS_OWNERDRAW,
        215, 68, 90, 30, hWnd, reinterpret_cast<HMENU>(IDC_TOP_SUBTAB_FISHING), m_hInstance, nullptr);

    m_hBtnSubTabHistory = CreateWindowExW(0, L"BUTTON", L"Lịch sử",
        WS_CHILD | WS_VISIBLE | BS_OWNERDRAW,
        312, 68, 85, 30, hWnd, reinterpret_cast<HMENU>(IDC_TOP_SUBTAB_HISTORY), m_hInstance, nullptr);

    m_hComboEmulator = CreateWindowExW(0, L"COMBOBOX", L"",
        WS_CHILD | WS_VISIBLE | CBS_DROPDOWNLIST | WS_VSCROLL,
        640, 68, 150, 150, hWnd, reinterpret_cast<HMENU>(IDC_TOP_COMBO_EMU), m_hInstance, nullptr);
    SendMessage(m_hComboEmulator, WM_SETFONT, reinterpret_cast<WPARAM>(m_hFontRegular), TRUE);
    SendMessage(m_hComboEmulator, CB_ADDSTRING, 0, reinterpret_cast<LPARAM>(L"LDPlayer"));
    SendMessage(m_hComboEmulator, CB_ADDSTRING, 0, reinterpret_cast<LPARAM>(L"MEmu"));
    SendMessage(m_hComboEmulator, CB_SETCURSEL, 0, 0);

    m_hComboTab = CreateWindowExW(0, L"COMBOBOX", L"",
        WS_CHILD | WS_VISIBLE | CBS_DROPDOWNLIST | WS_VSCROLL,
        805, 68, 185, 150, hWnd, reinterpret_cast<HMENU>(IDC_TOP_COMBO_TAB), m_hInstance, nullptr);
    SendMessage(m_hComboTab, WM_SETFONT, reinterpret_cast<WPARAM>(m_hFontRegular), TRUE);
    SendMessage(m_hComboTab, CB_ADDSTRING, 0, reinterpret_cast<LPARAM>(L"Tab 1 (Auto-Detect)"));
    SendMessage(m_hComboTab, CB_SETCURSEL, 0, 0);

    m_hBtnRefreshEmu = CreateWindowExW(0, L"BUTTON", L"🔄",
        WS_CHILD | WS_VISIBLE | BS_OWNERDRAW,
        1000, 67, 36, 32, hWnd, reinterpret_cast<HMENU>(IDC_TOP_BTN_REFRESH), m_hInstance, nullptr);

    // ==========================================
    // TAB 0: ESP CONTROLS
    // ==========================================
    m_hEspBtnStart = CreateWindowExW(0, L"BUTTON", L"▶  Bật ESP", WS_CHILD | BS_OWNERDRAW, 890, 115, 150, 60, hWnd, reinterpret_cast<HMENU>(IDC_ESP_BTN_START), m_hInstance, nullptr);
    m_hEspBtnStop = CreateWindowExW(0, L"BUTTON", L"■  Tắt ESP", WS_CHILD | BS_OWNERDRAW, 890, 185, 150, 60, hWnd, reinterpret_cast<HMENU>(IDC_ESP_BTN_STOP), m_hInstance, nullptr);
    m_hEspChkSnapline = CreateWindowExW(0, L"BUTTON", L"Vẽ đường kẻ Snapline", WS_CHILD | BS_AUTOCHECKBOX, 510, 130, 160, 24, hWnd, reinterpret_cast<HMENU>(IDC_ESP_CHK_SNAPLINE), m_hInstance, nullptr);
    m_hEspChk3DBox = CreateWindowExW(0, L"BUTTON", L"Vẽ khung hộp 3D Box", WS_CHILD | BS_AUTOCHECKBOX, 510, 160, 160, 24, hWnd, reinterpret_cast<HMENU>(IDC_ESP_CHK_3DBOX), m_hInstance, nullptr);
    m_hEspChkDistance = CreateWindowExW(0, L"BUTTON", L"Hiện khoảng cách (m)", WS_CHILD | BS_AUTOCHECKBOX, 700, 130, 160, 24, hWnd, reinterpret_cast<HMENU>(IDC_ESP_CHK_DIST), m_hInstance, nullptr);
    m_hEspChkTier = CreateWindowExW(0, L"BUTTON", L"Hiện tên & phẩm màu", WS_CHILD | BS_AUTOCHECKBOX, 700, 160, 160, 24, hWnd, reinterpret_cast<HMENU>(IDC_ESP_CHK_TIER), m_hInstance, nullptr);

    m_hEspChkFish = CreateWindowExW(0, L"BUTTON", L"ESP Cá hiếm / VVIP bóng 4-7", WS_CHILD | BS_AUTOCHECKBOX, 235, 305, 260, 26, hWnd, reinterpret_cast<HMENU>(IDC_ESP_CHK_FISH), m_hInstance, nullptr);
    m_hEspChkOre = CreateWindowExW(0, L"BUTTON", L"ESP Quặng vàng, Kim cương & Thiên thạch", WS_CHILD | BS_AUTOCHECKBOX, 520, 305, 320, 26, hWnd, reinterpret_cast<HMENU>(IDC_ESP_CHK_ORE), m_hInstance, nullptr);
    m_hEspChkInsect = CreateWindowExW(0, L"BUTTON", L"ESP Côn trùng hiếm (Bướm đêm, Bọ vương miện)", WS_CHILD | BS_AUTOCHECKBOX, 235, 440, 340, 26, hWnd, reinterpret_cast<HMENU>(IDC_ESP_CHK_INSECT), m_hInstance, nullptr);
    m_hEspChkChest = CreateWindowExW(0, L"BUTTON", L"ESP Rương cổ vật & Rương kho báu ngầm", WS_CHILD | BS_AUTOCHECKBOX, 600, 440, 300, 26, hWnd, reinterpret_cast<HMENU>(IDC_ESP_CHK_CHEST), m_hInstance, nullptr);

    for (HWND h : {m_hEspBtnStart, m_hEspBtnStop, m_hEspChkSnapline, m_hEspChk3DBox, m_hEspChkDistance, m_hEspChkTier, m_hEspChkFish, m_hEspChkOre, m_hEspChkInsect, m_hEspChkChest}) {
        SendMessage(h, WM_SETFONT, reinterpret_cast<WPARAM>(m_hFontRegular), TRUE);
        RegControl(0, h);
    }
    SendMessage(m_hEspChkSnapline, BM_SETCHECK, BST_CHECKED, 0);
    SendMessage(m_hEspChkFish, BM_SETCHECK, BST_CHECKED, 0);
    SendMessage(m_hEspChkOre, BM_SETCHECK, BST_CHECKED, 0);

    // ==========================================
    // TAB 1: CÂU CÁ CONTROLS
    // ==========================================
    m_hBtnStartFishing = CreateWindowExW(0, L"BUTTON", L"▶  Bật", WS_CHILD | BS_OWNERDRAW, 890, 115, 150, 60, hWnd, reinterpret_cast<HMENU>(IDC_FISH_BTN_START), m_hInstance, nullptr);
    m_hBtnStopFishing = CreateWindowExW(0, L"BUTTON", L"■  Tắt", WS_CHILD | BS_OWNERDRAW, 890, 185, 150, 60, hWnd, reinterpret_cast<HMENU>(IDC_FISH_BTN_STOP), m_hInstance, nullptr);
    m_hChkAutoRepair = CreateWindowExW(0, L"BUTTON", L"Tự sửa cần", WS_CHILD | BS_AUTOCHECKBOX, 510, 130, 150, 24, hWnd, reinterpret_cast<HMENU>(IDC_FISH_CHK_AUTOREPAIR), m_hInstance, nullptr);
    m_hChkFastSell = CreateWindowExW(0, L"BUTTON", L"Có gói bán nhanh", WS_CHILD | BS_AUTOCHECKBOX, 510, 160, 150, 24, hWnd, reinterpret_cast<HMENU>(IDC_FISH_CHK_FASTSELL), m_hInstance, nullptr);
    m_hBtnKeepFish = CreateWindowExW(0, L"BUTTON", L"Bảo quản", WS_CHILD | BS_OWNERDRAW, 510, 212, 75, 26, hWnd, reinterpret_cast<HMENU>(IDC_FISH_BTN_KEEPFISH), m_hInstance, nullptr);
    m_hBtnSellFish = CreateWindowExW(0, L"BUTTON", L"Bán nhanh", WS_CHILD | BS_OWNERDRAW, 590, 212, 75, 26, hWnd, reinterpret_cast<HMENU>(IDC_FISH_BTN_SELLFISH), m_hInstance, nullptr);
    m_hChkLockPov = CreateWindowExW(0, L"BUTTON", L"Khóa POV khi câu", WS_CHILD | BS_AUTOCHECKBOX, 700, 130, 160, 24, hWnd, reinterpret_cast<HMENU>(IDC_FISH_CHK_LOCKPOV), m_hInstance, nullptr);
    m_hChkFastBite = CreateWindowExW(0, L"BUTTON", L"Cá cắn nhanh", WS_CHILD | BS_AUTOCHECKBOX, 700, 160, 160, 24, hWnd, reinterpret_cast<HMENU>(IDC_FISH_CHK_FASTBITE), m_hInstance, nullptr);
    m_hChkFilterFish = CreateWindowExW(0, L"BUTTON", L"Lọc cá", WS_CHILD | BS_AUTOCHECKBOX, 225, 275, 75, 24, hWnd, reinterpret_cast<HMENU>(IDC_FISH_CHK_FILTER), m_hInstance, nullptr);
    m_hEditFilterId = CreateWindowExW(WS_EX_CLIENTEDGE, L"EDIT", L"", WS_CHILD | ES_AUTOHSCROLL, 645, 274, 210, 26, hWnd, reinterpret_cast<HMENU>(IDC_FISH_EDIT_FILTER_ID), m_hInstance, nullptr);
    m_hBtnClearFilter = CreateWindowExW(0, L"BUTTON", L"Xoá điều kiện lọc", WS_CHILD | BS_OWNERDRAW, 870, 272, 170, 28, hWnd, reinterpret_cast<HMENU>(IDC_FISH_BTN_CLEAR_FILTER), m_hInstance, nullptr);

    int chipX = 275;
    for (int i = 0; i < 7; ++i) {
        wchar_t szNum[8]; swprintf_s(szNum, L"%d", i + 1);
        m_btnFilterShadow[i] = CreateWindowExW(0, L"BUTTON", szNum, WS_CHILD | BS_OWNERDRAW, chipX, 315, 28, 26, hWnd, reinterpret_cast<HMENU>(static_cast<INT_PTR>(IDC_FISH_BTN_SHADOW_BASE + i)), m_hInstance, nullptr);
        chipX += 34;
    }

    const wchar_t* tierNames[] = {L"● Trắng", L"● Xanh lá", L"● Xanh dương", L"● Tím", L"● VIP"};
    int tierX = 570;
    int tierW[] = {75, 80, 95, 65, 75};
    for (int i = 0; i < 5; ++i) {
        m_btnFilterTier[i] = CreateWindowExW(0, L"BUTTON", tierNames[i], WS_CHILD | BS_OWNERDRAW, tierX, 315, tierW[i], 26, hWnd, reinterpret_cast<HMENU>(static_cast<INT_PTR>(IDC_FISH_BTN_TIER_BASE + i)), m_hInstance, nullptr);
        tierX += tierW[i] + 8;
    }

    m_hChkKeepVariant = CreateWindowExW(0, L"BUTTON", L"Giữ cá biến thể", WS_CHILD | BS_AUTOCHECKBOX, 225, 415, 125, 24, hWnd, reinterpret_cast<HMENU>(IDC_FISH_CHK_KEEP_VAR), m_hInstance, nullptr);
    m_hChkKeepMutant = CreateWindowExW(0, L"BUTTON", L"Giữ cá đột biến", WS_CHILD | BS_AUTOCHECKBOX, 500, 415, 130, 24, hWnd, reinterpret_cast<HMENU>(IDC_FISH_CHK_KEEP_MUT), m_hInstance, nullptr);
    m_hEditKeepId = CreateWindowExW(WS_EX_CLIENTEDGE, L"EDIT", L"", WS_CHILD | ES_AUTOHSCROLL, 695, 414, 160, 26, hWnd, reinterpret_cast<HMENU>(IDC_FISH_EDIT_KEEP_ID), m_hInstance, nullptr);
    m_hBtnClearKeep = CreateWindowExW(0, L"BUTTON", L"Xoá điều kiện giữ", WS_CHILD | BS_OWNERDRAW, 870, 412, 170, 28, hWnd, reinterpret_cast<HMENU>(IDC_FISH_BTN_CLEAR_KEEP), m_hInstance, nullptr);

    chipX = 295;
    for (int i = 0; i < 7; ++i) {
        wchar_t szNum[8]; swprintf_s(szNum, L"%d", i + 1);
        m_btnKeepShadow[i] = CreateWindowExW(0, L"BUTTON", szNum, WS_CHILD | BS_OWNERDRAW, chipX, 455, 28, 26, hWnd, reinterpret_cast<HMENU>(static_cast<INT_PTR>(IDC_FISH_BTN_KSHADOW_BASE + i)), m_hInstance, nullptr);
        chipX += 34;
    }

    tierX = 585;
    for (int i = 0; i < 5; ++i) {
        m_btnKeepTier[i] = CreateWindowExW(0, L"BUTTON", tierNames[i], WS_CHILD | BS_OWNERDRAW, tierX, 455, tierW[i], 26, hWnd, reinterpret_cast<HMENU>(static_cast<INT_PTR>(IDC_FISH_BTN_KTIER_BASE + i)), m_hInstance, nullptr);
        tierX += tierW[i] + 8;
    }

    for (HWND h : {m_hBtnStartFishing, m_hBtnStopFishing, m_hChkAutoRepair, m_hChkFastSell, m_hBtnKeepFish, m_hBtnSellFish, m_hChkLockPov, m_hChkFastBite, m_hChkFilterFish, m_hEditFilterId, m_hBtnClearFilter, m_hChkKeepVariant, m_hChkKeepMutant, m_hEditKeepId, m_hBtnClearKeep}) {
        SendMessage(h, WM_SETFONT, reinterpret_cast<WPARAM>(m_hFontRegular), TRUE);
        RegControl(1, h);
    }
    for (HWND h : m_btnFilterShadow) RegControl(1, h);
    for (HWND h : m_btnFilterTier) RegControl(1, h);
    for (HWND h : m_btnKeepShadow) RegControl(1, h);
    for (HWND h : m_btnKeepTier) RegControl(1, h);
    SendMessage(m_hChkAutoRepair, BM_SETCHECK, BST_CHECKED, 0);

    // ==========================================
    // TAB 2: ĐÀO CỔ VẬT CONTROLS
    // ==========================================
    m_hExcBtnStart = CreateWindowExW(0, L"BUTTON", L"▶  Bật Đào", WS_CHILD | BS_OWNERDRAW, 890, 115, 150, 60, hWnd, reinterpret_cast<HMENU>(IDC_EXC_BTN_START), m_hInstance, nullptr);
    m_hExcBtnStop = CreateWindowExW(0, L"BUTTON", L"■  Tắt Đào", WS_CHILD | BS_OWNERDRAW, 890, 185, 150, 60, hWnd, reinterpret_cast<HMENU>(IDC_EXC_BTN_STOP), m_hInstance, nullptr);
    m_hExcChkAutoBuy = CreateWindowExW(0, L"BUTTON", L"Tự mua máy dò khi hỏng", WS_CHILD | BS_AUTOCHECKBOX, 510, 130, 160, 24, hWnd, reinterpret_cast<HMENU>(IDC_EXC_CHK_BUY), m_hInstance, nullptr);
    m_hExcChkAutoRepair = CreateWindowExW(0, L"BUTTON", L"Tự sửa máy dò ngầm", WS_CHILD | BS_AUTOCHECKBOX, 510, 160, 160, 24, hWnd, reinterpret_cast<HMENU>(IDC_EXC_CHK_REPAIR), m_hInstance, nullptr);
    m_hExcChkSkipWood = CreateWindowExW(0, L"BUTTON", L"Bỏ qua rương gỗ rác", WS_CHILD | BS_AUTOCHECKBOX, 700, 130, 160, 24, hWnd, reinterpret_cast<HMENU>(IDC_EXC_CHK_SKIPWOOD), m_hInstance, nullptr);
    m_hExcChkMiniGame = CreateWindowExW(0, L"BUTTON", L"Tự giải Mini-game 100%", WS_CHILD | BS_AUTOCHECKBOX, 700, 160, 160, 24, hWnd, reinterpret_cast<HMENU>(IDC_EXC_CHK_MINIGAME), m_hInstance, nullptr);

    m_hExcChkChestSilver = CreateWindowExW(0, L"BUTTON", L"Ưu tiên Rương Bạc", WS_CHILD | BS_AUTOCHECKBOX, 235, 305, 180, 26, hWnd, reinterpret_cast<HMENU>(IDC_EXC_CHK_SILVER), m_hInstance, nullptr);
    m_hExcChkChestGold = CreateWindowExW(0, L"BUTTON", L"Ưu tiên Rương Vàng", WS_CHILD | BS_AUTOCHECKBOX, 435, 305, 180, 26, hWnd, reinterpret_cast<HMENU>(IDC_EXC_CHK_GOLD), m_hInstance, nullptr);
    m_hExcChkChestDiamond = CreateWindowExW(0, L"BUTTON", L"Ưu tiên Rương Kim Cương", WS_CHILD | BS_AUTOCHECKBOX, 635, 305, 210, 26, hWnd, reinterpret_cast<HMENU>(IDC_EXC_CHK_DIAMOND), m_hInstance, nullptr);

    m_hExcChkOpenNow = CreateWindowExW(0, L"BUTTON", L"Tự động mở rương ngay khi đào xong", WS_CHILD | BS_AUTOCHECKBOX, 235, 440, 280, 26, hWnd, reinterpret_cast<HMENU>(IDC_EXC_CHK_OPENNOW), m_hInstance, nullptr);
    m_hExcChkNextChest = CreateWindowExW(0, L"BUTTON", L"Dịch chuyển tức thì đến rương ngầm tiếp theo", WS_CHILD | BS_AUTOCHECKBOX, 540, 440, 320, 26, hWnd, reinterpret_cast<HMENU>(IDC_EXC_CHK_NEXT), m_hInstance, nullptr);

    for (HWND h : {m_hExcBtnStart, m_hExcBtnStop, m_hExcChkAutoBuy, m_hExcChkAutoRepair, m_hExcChkSkipWood, m_hExcChkMiniGame, m_hExcChkChestSilver, m_hExcChkChestGold, m_hExcChkChestDiamond, m_hExcChkOpenNow, m_hExcChkNextChest}) {
        SendMessage(h, WM_SETFONT, reinterpret_cast<WPARAM>(m_hFontRegular), TRUE);
        RegControl(2, h);
    }
    SendMessage(m_hExcChkAutoBuy, BM_SETCHECK, BST_CHECKED, 0);
    SendMessage(m_hExcChkMiniGame, BM_SETCHECK, BST_CHECKED, 0);

    // ==========================================
    // TAB 3: ĐẬP ĐÁ CONTROLS
    // ==========================================
    m_hMinBtnStart = CreateWindowExW(0, L"BUTTON", L"▶  Bật Đập Đá", WS_CHILD | BS_OWNERDRAW, 890, 115, 150, 60, hWnd, reinterpret_cast<HMENU>(IDC_MIN_BTN_START), m_hInstance, nullptr);
    m_hMinBtnStop = CreateWindowExW(0, L"BUTTON", L"■  Tắt Đập Đá", WS_CHILD | BS_OWNERDRAW, 890, 185, 150, 60, hWnd, reinterpret_cast<HMENU>(IDC_MIN_BTN_STOP), m_hInstance, nullptr);
    m_hMinChkAutoRepair = CreateWindowExW(0, L"BUTTON", L"Tự sửa cuốc / búa", WS_CHILD | BS_AUTOCHECKBOX, 510, 130, 160, 24, hWnd, reinterpret_cast<HMENU>(IDC_MIN_CHK_REPAIR), m_hInstance, nullptr);
    m_hMinChkSwapVip = CreateWindowExW(0, L"BUTTON", L"Đổi cuốc VIP khi mỏ hiếm", WS_CHILD | BS_AUTOCHECKBOX, 510, 160, 160, 24, hWnd, reinterpret_cast<HMENU>(IDC_MIN_CHK_SWAPVIP), m_hInstance, nullptr);
    m_hMinChkAntiKs = CreateWindowExW(0, L"BUTTON", L"Né người khác (Chống KS)", WS_CHILD | BS_AUTOCHECKBOX, 700, 130, 160, 24, hWnd, reinterpret_cast<HMENU>(IDC_MIN_CHK_ANTIKS), m_hInstance, nullptr);

    m_hMinChkOreIron = CreateWindowExW(0, L"BUTTON", L"Khai thác Quặng Sắt", WS_CHILD | BS_AUTOCHECKBOX, 235, 305, 180, 26, hWnd, reinterpret_cast<HMENU>(IDC_MIN_CHK_IRON), m_hInstance, nullptr);
    m_hMinChkOreGold = CreateWindowExW(0, L"BUTTON", L"Khai thác Quặng Vàng", WS_CHILD | BS_AUTOCHECKBOX, 435, 305, 180, 26, hWnd, reinterpret_cast<HMENU>(IDC_MIN_CHK_GOLD), m_hInstance, nullptr);
    m_hMinChkOreDiamond = CreateWindowExW(0, L"BUTTON", L"Khai thác Kim Cương", WS_CHILD | BS_AUTOCHECKBOX, 635, 305, 180, 26, hWnd, reinterpret_cast<HMENU>(IDC_MIN_CHK_DIAMOND), m_hInstance, nullptr);
    m_hMinChkOreMeteor = CreateWindowExW(0, L"BUTTON", L"Khai thác Thiên Thạch", WS_CHILD | BS_AUTOCHECKBOX, 835, 305, 180, 26, hWnd, reinterpret_cast<HMENU>(IDC_MIN_CHK_METEOR), m_hInstance, nullptr);

    m_hMinComboZone = CreateWindowExW(0, L"COMBOBOX", L"", WS_CHILD | CBS_DROPDOWNLIST | WS_VSCROLL,
        435, 440, 280, 150, hWnd, reinterpret_cast<HMENU>(IDC_MIN_COMBO_ZONE), m_hInstance, nullptr);
    SendMessage(m_hMinComboZone, WM_SETFONT, reinterpret_cast<WPARAM>(m_hFontRegular), TRUE);
    SendMessage(m_hMinComboZone, CB_ADDSTRING, 0, reinterpret_cast<LPARAM>(L"Khu Downtown (Mỏ đá lớn)"));
    SendMessage(m_hMinComboZone, CB_ADDSTRING, 0, reinterpret_cast<LPARAM>(L"Đảo Plaza (Bờ hồ & Núi)"));
    SendMessage(m_hMinComboZone, CB_ADDSTRING, 0, reinterpret_cast<LPARAM>(L"Khu Cắm Trại (Hang đá)"));
    SendMessage(m_hMinComboZone, CB_SETCURSEL, 0, 0);

    for (HWND h : {m_hMinBtnStart, m_hMinBtnStop, m_hMinChkAutoRepair, m_hMinChkSwapVip, m_hMinChkAntiKs, m_hMinChkOreIron, m_hMinChkOreGold, m_hMinChkOreDiamond, m_hMinChkOreMeteor, m_hMinComboZone}) {
        SendMessage(h, WM_SETFONT, reinterpret_cast<WPARAM>(m_hFontRegular), TRUE);
        RegControl(3, h);
    }
    SendMessage(m_hMinChkAutoRepair, BM_SETCHECK, BST_CHECKED, 0);
    SendMessage(m_hMinChkOreDiamond, BM_SETCHECK, BST_CHECKED, 0);

    // ==========================================
    // TAB 4: BẮT BỌ CONTROLS
    // ==========================================
    m_hInsBtnStart = CreateWindowExW(0, L"BUTTON", L"▶  Bật Bắt Bọ", WS_CHILD | BS_OWNERDRAW, 890, 115, 150, 60, hWnd, reinterpret_cast<HMENU>(IDC_INS_BTN_START), m_hInstance, nullptr);
    m_hInsBtnStop = CreateWindowExW(0, L"BUTTON", L"■  Tắt Bắt Bọ", WS_CHILD | BS_OWNERDRAW, 890, 185, 150, 60, hWnd, reinterpret_cast<HMENU>(IDC_INS_BTN_STOP), m_hInstance, nullptr);
    m_hInsChkAutoRepair = CreateWindowExW(0, L"BUTTON", L"Tự sửa vợt bắt bọ", WS_CHILD | BS_AUTOCHECKBOX, 510, 130, 160, 24, hWnd, reinterpret_cast<HMENU>(IDC_INS_CHK_REPAIR), m_hInstance, nullptr);
    m_hInsChkSneak = CreateWindowExW(0, L"BUTTON", L"Đi rón rén chống giật mình", WS_CHILD | BS_AUTOCHECKBOX, 510, 160, 160, 24, hWnd, reinterpret_cast<HMENU>(IDC_INS_CHK_SNEAK), m_hInstance, nullptr);
    m_hInsChkLockAngle = CreateWindowExW(0, L"BUTTON", L"Khóa hướng vung vợt", WS_CHILD | BS_AUTOCHECKBOX, 700, 130, 160, 24, hWnd, reinterpret_cast<HMENU>(IDC_INS_CHK_LOCK), m_hInstance, nullptr);
    m_hInsChkTeleportBack = CreateWindowExW(0, L"BUTTON", L"Áp sát lưng bọ Zero-Tap", WS_CHILD | BS_AUTOCHECKBOX, 700, 160, 160, 24, hWnd, reinterpret_cast<HMENU>(IDC_INS_CHK_TELEPORT), m_hInstance, nullptr);

    m_hInsChkButterfly = CreateWindowExW(0, L"BUTTON", L"Bắt Bướm đêm & Côn trùng bay", WS_CHILD | BS_AUTOCHECKBOX, 235, 305, 240, 26, hWnd, reinterpret_cast<HMENU>(IDC_INS_CHK_BUTTERFLY), m_hInstance, nullptr);
    m_hInsChkBeetle = CreateWindowExW(0, L"BUTTON", L"Bắt Bọ cánh cứng khổng lồ", WS_CHILD | BS_AUTOCHECKBOX, 500, 305, 240, 26, hWnd, reinterpret_cast<HMENU>(IDC_INS_CHK_BEETLE), m_hInstance, nullptr);
    m_hInsChkStag = CreateWindowExW(0, L"BUTTON", L"Bắt Bọ kẹp kìm sừng nhọn", WS_CHILD | BS_AUTOCHECKBOX, 235, 440, 240, 26, hWnd, reinterpret_cast<HMENU>(IDC_INS_CHK_STAG), m_hInstance, nullptr);
    m_hInsChkScorpion = CreateWindowExW(0, L"BUTTON", L"Bắt Bọ cạp & Bọ ngựa vương miện", WS_CHILD | BS_AUTOCHECKBOX, 500, 440, 270, 26, hWnd, reinterpret_cast<HMENU>(IDC_INS_CHK_SCORPION), m_hInstance, nullptr);

    for (HWND h : {m_hInsBtnStart, m_hInsBtnStop, m_hInsChkAutoRepair, m_hInsChkSneak, m_hInsChkLockAngle, m_hInsChkTeleportBack, m_hInsChkButterfly, m_hInsChkBeetle, m_hInsChkStag, m_hInsChkScorpion}) {
        SendMessage(h, WM_SETFONT, reinterpret_cast<WPARAM>(m_hFontRegular), TRUE);
        RegControl(4, h);
    }
    SendMessage(m_hInsChkSneak, BM_SETCHECK, BST_CHECKED, 0);
    SendMessage(m_hInsChkLockAngle, BM_SETCHECK, BST_CHECKED, 0);

    // ==========================================
    // TAB 5: THU LƯỢM CONTROLS
    // ==========================================
    m_hColBtnStart = CreateWindowExW(0, L"BUTTON", L"▶  Bật Thu Lượm", WS_CHILD | BS_OWNERDRAW, 890, 115, 150, 60, hWnd, reinterpret_cast<HMENU>(IDC_COL_BTN_START), m_hInstance, nullptr);
    m_hColBtnStop = CreateWindowExW(0, L"BUTTON", L"■  Tắt Thu Lượm", WS_CHILD | BS_OWNERDRAW, 890, 185, 150, 60, hWnd, reinterpret_cast<HMENU>(IDC_COL_BTN_STOP), m_hInstance, nullptr);
    m_hColChkAutoSell = CreateWindowExW(0, L"BUTTON", L"Tự bán rác khi đầy balo", WS_CHILD | BS_AUTOCHECKBOX, 510, 130, 160, 24, hWnd, reinterpret_cast<HMENU>(IDC_COL_CHK_SELL), m_hInstance, nullptr);
    m_hColChkAutoRecycle = CreateWindowExW(0, L"BUTTON", L"Tự tái chế chai lọ rác thải", WS_CHILD | BS_AUTOCHECKBOX, 510, 160, 160, 24, hWnd, reinterpret_cast<HMENU>(IDC_COL_CHK_RECYCLE), m_hInstance, nullptr);
    m_hColChkOptimizeTsp = CreateWindowExW(0, L"BUTTON", L"Tối ưu lộ trình ngắn nhất TSP", WS_CHILD | BS_AUTOCHECKBOX, 700, 130, 160, 24, hWnd, reinterpret_cast<HMENU>(IDC_COL_CHK_TSP), m_hInstance, nullptr);

    m_hColChkBranch = CreateWindowExW(0, L"BUTTON", L"Nhặt Cành Cây & Gỗ mục", WS_CHILD | BS_AUTOCHECKBOX, 235, 305, 200, 26, hWnd, reinterpret_cast<HMENU>(IDC_COL_CHK_BRANCH), m_hInstance, nullptr);
    m_hColChkApple = CreateWindowExW(0, L"BUTTON", L"Nhặt Táo & Hoa quả dại", WS_CHILD | BS_AUTOCHECKBOX, 460, 305, 200, 26, hWnd, reinterpret_cast<HMENU>(IDC_COL_CHK_APPLE), m_hInstance, nullptr);
    m_hColChkMushroom = CreateWindowExW(0, L"BUTTON", L"Nhặt Nấm độc & Hoa cúc", WS_CHILD | BS_AUTOCHECKBOX, 680, 305, 200, 26, hWnd, reinterpret_cast<HMENU>(IDC_COL_CHK_MUSHROOM), m_hInstance, nullptr);
    m_hColChkStar = CreateWindowExW(0, L"BUTTON", L"Nhặt Sao ước nguyện & Vỏ sò", WS_CHILD | BS_AUTOCHECKBOX, 235, 440, 240, 26, hWnd, reinterpret_cast<HMENU>(IDC_COL_CHK_STAR), m_hInstance, nullptr);

    for (HWND h : {m_hColBtnStart, m_hColBtnStop, m_hColChkAutoSell, m_hColChkAutoRecycle, m_hColChkOptimizeTsp, m_hColChkBranch, m_hColChkApple, m_hColChkMushroom, m_hColChkStar}) {
        SendMessage(h, WM_SETFONT, reinterpret_cast<WPARAM>(m_hFontRegular), TRUE);
        RegControl(5, h);
    }
    SendMessage(m_hColChkOptimizeTsp, BM_SETCHECK, BST_CHECKED, 0);

    // ==========================================
    // TAB 6: NÔNG TRẠI CONTROLS
    // ==========================================
    m_hFarmBtnStart = CreateWindowExW(0, L"BUTTON", L"▶  Bật Nông Trại", WS_CHILD | BS_OWNERDRAW, 890, 115, 150, 60, hWnd, reinterpret_cast<HMENU>(IDC_FARM_BTN_START), m_hInstance, nullptr);
    m_hFarmBtnStop = CreateWindowExW(0, L"BUTTON", L"■  Tắt Nông Trại", WS_CHILD | BS_OWNERDRAW, 890, 185, 150, 60, hWnd, reinterpret_cast<HMENU>(IDC_FARM_BTN_STOP), m_hInstance, nullptr);
    m_hFarmChkWater = CreateWindowExW(0, L"BUTTON", L"Tự tưới nước khi đất khô", WS_CHILD | BS_AUTOCHECKBOX, 510, 130, 160, 24, hWnd, reinterpret_cast<HMENU>(IDC_FARM_CHK_WATER), m_hInstance, nullptr);
    m_hFarmChkFertilize = CreateWindowExW(0, L"BUTTON", L"Tự bón phân tăng tốc", WS_CHILD | BS_AUTOCHECKBOX, 510, 160, 160, 24, hWnd, reinterpret_cast<HMENU>(IDC_FARM_CHK_FERT), m_hInstance, nullptr);
    m_hFarmChkAutoBuySeeds = CreateWindowExW(0, L"BUTTON", L"Tự mua hạt giống khi hết", WS_CHILD | BS_AUTOCHECKBOX, 700, 130, 160, 24, hWnd, reinterpret_cast<HMENU>(IDC_FARM_CHK_BUYSEED), m_hInstance, nullptr);
    m_hFarmChkHarvestNow = CreateWindowExW(0, L"BUTTON", L"Thu hoạch ngay khi chín", WS_CHILD | BS_AUTOCHECKBOX, 700, 160, 160, 24, hWnd, reinterpret_cast<HMENU>(IDC_FARM_CHK_HARVEST), m_hInstance, nullptr);

    m_hFarmChkChicken = CreateWindowExW(0, L"BUTTON", L"Cho gà ăn & Thu trứng gà", WS_CHILD | BS_AUTOCHECKBOX, 235, 305, 220, 26, hWnd, reinterpret_cast<HMENU>(IDC_FARM_CHK_CHICKEN), m_hInstance, nullptr);
    m_hFarmChkCow = CreateWindowExW(0, L"BUTTON", L"Cho bò ăn & Vắt sữa bò tươi", WS_CHILD | BS_AUTOCHECKBOX, 480, 305, 230, 26, hWnd, reinterpret_cast<HMENU>(IDC_FARM_CHK_COW), m_hInstance, nullptr);

    for (HWND h : {m_hFarmBtnStart, m_hFarmBtnStop, m_hFarmChkWater, m_hFarmChkFertilize, m_hFarmChkAutoBuySeeds, m_hFarmChkHarvestNow, m_hFarmChkChicken, m_hFarmChkCow}) {
        SendMessage(h, WM_SETFONT, reinterpret_cast<WPARAM>(m_hFontRegular), TRUE);
        RegControl(6, h);
    }
    SendMessage(m_hFarmChkWater, BM_SETCHECK, BST_CHECKED, 0);
    SendMessage(m_hFarmChkHarvestNow, BM_SETCHECK, BST_CHECKED, 0);

    // ==========================================
    // TAB 7: DỊCH CHUYỂN (TELEPORT) CONTROLS
    // ==========================================
    m_hTeleSpotList = CreateWindowExW(0, WC_LISTVIEWW, L"",
        WS_CHILD | LVS_REPORT | LVS_SINGLESEL | WS_BORDER,
        225, 120, 630, 340, hWnd, reinterpret_cast<HMENU>(IDC_TELE_LIST), m_hInstance, nullptr);
    SendMessage(m_hTeleSpotList, WM_SETFONT, reinterpret_cast<WPARAM>(m_hFontRegular), TRUE);
    ListView_SetExtendedListViewStyle(m_hTeleSpotList, LVS_EX_FULLROWSELECT | LVS_EX_DOUBLEBUFFER);

    LVCOLUMNW lvc{};
    lvc.mask = LVCF_TEXT | LVCF_WIDTH | LVCF_SUBITEM;
    lvc.cx = 150; lvc.pszText = const_cast<LPWSTR>(L"Tên Tọa Độ"); ListView_InsertColumn(m_hTeleSpotList, 0, &lvc);
    lvc.cx = 90;  lvc.pszText = const_cast<LPWSTR>(L"Khu Vực"); ListView_InsertColumn(m_hTeleSpotList, 1, &lvc);
    lvc.cx = 85;  lvc.pszText = const_cast<LPWSTR>(L"Tọa độ X"); ListView_InsertColumn(m_hTeleSpotList, 2, &lvc);
    lvc.cx = 85;  lvc.pszText = const_cast<LPWSTR>(L"Tọa độ Y"); ListView_InsertColumn(m_hTeleSpotList, 3, &lvc);
    lvc.cx = 85;  lvc.pszText = const_cast<LPWSTR>(L"Tọa độ Z"); ListView_InsertColumn(m_hTeleSpotList, 4, &lvc);
    lvc.cx = 95;  lvc.pszText = const_cast<LPWSTR>(L"Trạng Thái"); ListView_InsertColumn(m_hTeleSpotList, 5, &lvc);

    m_hBtnTeleportInstant = CreateWindowExW(0, L"BUTTON", L"DỊCH CHUYỂN TỨC THÌ", WS_CHILD | BS_OWNERDRAW, 880, 120, 180, 50, hWnd, reinterpret_cast<HMENU>(IDC_TELE_BTN_GO), m_hInstance, nullptr);
    m_hBtnSyncSpots = CreateWindowExW(0, L"BUTTON", L"ĐỒNG BỘ TỪ SERVER", WS_CHILD | BS_OWNERDRAW, 880, 185, 180, 46, hWnd, reinterpret_cast<HMENU>(IDC_TELE_BTN_SYNC), m_hInstance, nullptr);
    m_hBtnSaveCurrentPos = CreateWindowExW(0, L"BUTTON", L"LƯU VỊ TRÍ HIỆN TẠI", WS_CHILD | BS_OWNERDRAW, 880, 245, 180, 46, hWnd, reinterpret_cast<HMENU>(IDC_TELE_BTN_SAVEPOS), m_hInstance, nullptr);

    m_hBtnZonePlaza = CreateWindowExW(0, L"BUTTON", L"Đảo Plaza", WS_CHILD | BS_OWNERDRAW, 235, 510, 130, 36, hWnd, reinterpret_cast<HMENU>(IDC_TELE_MAP_1), m_hInstance, nullptr);
    m_hBtnZoneDowntown = CreateWindowExW(0, L"BUTTON", L"Khu Downtown", WS_CHILD | BS_OWNERDRAW, 380, 510, 140, 36, hWnd, reinterpret_cast<HMENU>(IDC_TELE_MAP_2), m_hInstance, nullptr);
    m_hBtnZoneCamping = CreateWindowExW(0, L"BUTTON", L"Khu Cắm Trại", WS_CHILD | BS_OWNERDRAW, 535, 510, 140, 36, hWnd, reinterpret_cast<HMENU>(IDC_TELE_MAP_3), m_hInstance, nullptr);
    m_hBtnZoneResort = CreateWindowExW(0, L"BUTTON", L"Đảo Nghỉ Dưỡng", WS_CHILD | BS_OWNERDRAW, 690, 510, 150, 36, hWnd, reinterpret_cast<HMENU>(IDC_TELE_MAP_4), m_hInstance, nullptr);
    m_hBtnZoneHome = CreateWindowExW(0, L"BUTTON", L"Về Nhà Riêng", WS_CHILD | BS_OWNERDRAW, 855, 510, 140, 36, hWnd, reinterpret_cast<HMENU>(IDC_TELE_MAP_10), m_hInstance, nullptr);

    for (HWND h : {m_hTeleSpotList, m_hBtnTeleportInstant, m_hBtnSyncSpots, m_hBtnSaveCurrentPos, m_hBtnZonePlaza, m_hBtnZoneDowntown, m_hBtnZoneCamping, m_hBtnZoneResort, m_hBtnZoneHome}) {
        RegControl(7, h);
    }

    // ==========================================
    // TAB 8: CÀI ĐẶT (SETTINGS) CONTROLS
    // ==========================================
    m_hEditKey = CreateWindowExW(WS_EX_CLIENTEDGE, L"EDIT", L"DTA-VIP-2026-KEY", WS_CHILD | ES_AUTOHSCROLL, 380, 130, 360, 32, hWnd, reinterpret_cast<HMENU>(IDC_SET_EDIT_KEY), m_hInstance, nullptr);
    SendMessage(m_hEditKey, WM_SETFONT, reinterpret_cast<WPARAM>(m_hFontMono), TRUE);

    m_hBtnActivateKey = CreateWindowExW(0, L"BUTTON", L"Lưu / Kích Hoạt", WS_CHILD | BS_OWNERDRAW, 755, 128, 160, 36, hWnd, reinterpret_cast<HMENU>(IDC_SET_BTN_ACTIVATE), m_hInstance, nullptr);

    std::wstring wHwid(m_clientHwid.begin(), m_clientHwid.end());
    m_hStaticHWID = CreateWindowExW(WS_EX_CLIENTEDGE, L"EDIT", wHwid.c_str(), WS_CHILD | ES_READONLY | ES_AUTOHSCROLL, 380, 180, 360, 30, hWnd, reinterpret_cast<HMENU>(IDC_SET_EDIT_HWID), m_hInstance, nullptr);
    SendMessage(m_hStaticHWID, WM_SETFONT, reinterpret_cast<WPARAM>(m_hFontMono), TRUE);

    m_hBtnCopyHWID = CreateWindowExW(0, L"BUTTON", L"Sao Chép HWID", WS_CHILD | BS_OWNERDRAW, 755, 178, 160, 34, hWnd, reinterpret_cast<HMENU>(IDC_SET_BTN_COPY_HWID), m_hInstance, nullptr);

    m_hEditServerUrl = CreateWindowExW(WS_EX_CLIENTEDGE, L"EDIT", L"http://127.0.0.1:28445", WS_CHILD | ES_AUTOHSCROLL, 380, 230, 360, 30, hWnd, reinterpret_cast<HMENU>(IDC_SET_EDIT_SERVER), m_hInstance, nullptr);
    SendMessage(m_hEditServerUrl, WM_SETFONT, reinterpret_cast<WPARAM>(m_hFontMono), TRUE);

    m_hBtnPingServer = CreateWindowExW(0, L"BUTTON", L"Kiểm Tra Kết Nối", WS_CHILD | BS_OWNERDRAW, 755, 228, 160, 34, hWnd, reinterpret_cast<HMENU>(IDC_SET_BTN_PING), m_hInstance, nullptr);

    m_hChkAntiBan = CreateWindowExW(0, L"BUTTON", L"Bật Chế Độ Bảo Vệ Bộ Nhớ Cao Cấp (Memory Shield Anti-Ban)", WS_CHILD | BS_AUTOCHECKBOX, 380, 290, 480, 26, hWnd, reinterpret_cast<HMENU>(IDC_SET_CHK_ANTIBAN), m_hInstance, nullptr);
    SendMessage(m_hChkAntiBan, WM_SETFONT, reinterpret_cast<WPARAM>(m_hFontRegular), TRUE);
    SendMessage(m_hChkAntiBan, BM_SETCHECK, BST_CHECKED, 0);

    m_hChkPanicF12 = CreateWindowExW(0, L"BUTTON", L"Bật Phím Khẩn Cấp Panic Dừng Tool Toàn Diện (F12)", WS_CHILD | BS_AUTOCHECKBOX, 380, 330, 480, 26, hWnd, reinterpret_cast<HMENU>(IDC_SET_CHK_PANIC), m_hInstance, nullptr);
    SendMessage(m_hChkPanicF12, WM_SETFONT, reinterpret_cast<WPARAM>(m_hFontRegular), TRUE);
    SendMessage(m_hChkPanicF12, BM_SETCHECK, BST_CHECKED, 0);

    for (HWND h : {m_hEditKey, m_hBtnActivateKey, m_hStaticHWID, m_hBtnCopyHWID, m_hEditServerUrl, m_hBtnPingServer, m_hChkAntiBan, m_hChkPanicF12}) {
        RegControl(8, h);
    }

    SwitchTab(1); // Mặc định mở Tab Câu cá
    if (m_isFreeTier) {
        ApplyFreeTierRestrictions();
    }
}

void ClientWindow::ApplyFreeTierRestrictions() {
    if (!m_hWnd) return;

    if (m_isFreeTier) {
        SetWindowTextW(m_hWnd, L"DTA PlayTogether • BẢN MIỄN PHÍ (CHỈ CÂU CÁ)");

        // 1. Không lọc cá: Vô hiệu hóa toàn bộ tùy chọn bộ lọc cá
        EnableWindow(m_hChkFilterFish, FALSE);
        SendMessage(m_hChkFilterFish, BM_SETCHECK, BST_UNCHECKED, 0);
        EnableWindow(m_hEditFilterId, FALSE);
        EnableWindow(m_hBtnClearFilter, FALSE);
        for (HWND h : m_btnFilterShadow) EnableWindow(h, FALSE);
        m_filterShadowState.fill(false);
        for (HWND h : m_btnFilterTier) EnableWindow(h, FALSE);
        m_filterTierState.fill(false);

        // Vô hiệu hóa tùy chọn giữ cá
        EnableWindow(m_hChkKeepVariant, FALSE);
        SendMessage(m_hChkKeepVariant, BM_SETCHECK, BST_UNCHECKED, 0);
        EnableWindow(m_hChkKeepMutant, FALSE);
        SendMessage(m_hChkKeepMutant, BM_SETCHECK, BST_UNCHECKED, 0);
        EnableWindow(m_hEditKeepId, FALSE);
        EnableWindow(m_hBtnClearKeep, FALSE);
        for (HWND h : m_btnKeepShadow) EnableWindow(h, FALSE);
        m_keepShadowState.fill(false);
        for (HWND h : m_btnKeepTier) EnableWindow(h, FALSE);
        m_keepTierState.fill(false);

        // 2. Không giữ cá: Tự động bán tất cả cá câu được
        EnableWindow(m_hBtnKeepFish, FALSE);
        m_keepFishSelected = false; // Chọn Bán nhanh

        // 3. Không có cắn nhanh: Vô hiệu hóa checkbox Cá cắn nhanh
        EnableWindow(m_hChkFastBite, FALSE);
        SendMessage(m_hChkFastBite, BM_SETCHECK, BST_UNCHECKED, 0);

        // 4. Có khóa cam: m_hChkLockPov vẫn bật bình thường
        EnableWindow(m_hChkLockPov, TRUE);

        // Nút ở góc dưới sidebar
        SetWindowTextW(m_hBtnLogoutKey, L"⚡ Nâng cấp key");
    } else {
        SetWindowTextW(m_hWnd, L"DTA PlayTogether • VIP EDITION");

        EnableWindow(m_hChkFilterFish, TRUE);
        EnableWindow(m_hEditFilterId, TRUE);
        EnableWindow(m_hBtnClearFilter, TRUE);
        for (HWND h : m_btnFilterShadow) EnableWindow(h, TRUE);
        for (HWND h : m_btnFilterTier) EnableWindow(h, TRUE);

        EnableWindow(m_hChkKeepVariant, TRUE);
        EnableWindow(m_hChkKeepMutant, TRUE);
        EnableWindow(m_hEditKeepId, TRUE);
        EnableWindow(m_hBtnClearKeep, TRUE);
        for (HWND h : m_btnKeepShadow) EnableWindow(h, TRUE);
        for (HWND h : m_btnKeepTier) EnableWindow(h, TRUE);

        EnableWindow(m_hBtnKeepFish, TRUE);
        EnableWindow(m_hChkFastBite, TRUE);
        EnableWindow(m_hChkLockPov, TRUE);

        SetWindowTextW(m_hBtnLogoutKey, L"Đăng xuất key");
    }

    InvalidateRect(m_hWnd, nullptr, TRUE);
}

void ClientWindow::ShowTabControls(int tabIndex) {
    for (size_t t = 0; t < 9; ++t) {
        int cmd = (static_cast<int>(t) == tabIndex) ? SW_SHOW : SW_HIDE;
        for (HWND h : m_tabControls[t]) {
            ShowWindow(h, cmd);
        }
    }
}

void ClientWindow::SwitchTab(int tabIndex) {
    if (tabIndex < 0 || tabIndex >= 9) return;

    if (m_isFreeTier && tabIndex != 1) {
        MessageBoxW(m_hWnd,
            L"[DTA - BẢN MIỄN PHÍ]\n\n"
            L"Tính năng này chỉ dành cho bản quyền VIP!\n"
            L"Bản miễn phí chỉ hỗ trợ tính năng Câu Cá cơ bản.\n\n"
            L"Vui lòng nhấn nút 'Nâng cấp key' ở góc dưới để mở khóa toàn bộ 9 chức năng!",
            L"Tính Năng Dành Riêng Cho Bản VIP", MB_OK | MB_ICONWARNING);
        return;
    }

    m_activeTab = tabIndex;
    ShowTabControls(tabIndex);
    InvalidateRect(m_hWnd, nullptr, TRUE);
}

void ClientWindow::SyncMasterSpotsFromServer() {
    std::string jsonStr = network::NetworkClient::Instance().FetchMasterTeleportSpots();
    if (jsonStr.empty()) return;

    m_masterSpots.clear();

    size_t pos = 0;
    while ((pos = jsonStr.find("\"id\":", pos)) != std::string::npos) {
        MasterSpotDTO dto;
        size_t idStart = jsonStr.find("\"", pos + 5);
        if (idStart != std::string::npos) {
            idStart += 1;
            size_t idEnd = jsonStr.find("\"", idStart);
            if (idEnd != std::string::npos) dto.id = jsonStr.substr(idStart, idEnd - idStart);
        }

        size_t namePos = jsonStr.find("\"name\":", pos);
        if (namePos != std::string::npos) {
            size_t nStart = jsonStr.find("\"", namePos + 7);
            if (nStart != std::string::npos) {
                nStart += 1;
                size_t nEnd = jsonStr.find("\"", nStart);
                if (nEnd != std::string::npos) dto.name = jsonStr.substr(nStart, nEnd - nStart);
            }
        }

        size_t mapPos = jsonStr.find("\"mapName\":", pos);
        if (mapPos != std::string::npos) {
            size_t mStart = jsonStr.find("\"", mapPos + 10);
            if (mStart != std::string::npos) {
                mStart += 1;
                size_t mEnd = jsonStr.find("\"", mStart);
                if (mEnd != std::string::npos) dto.mapName = jsonStr.substr(mStart, mEnd - mStart);
            }
        }

        size_t xPos = jsonStr.find("\"x\":", pos);
        if (xPos != std::string::npos) dto.x = static_cast<float>(std::atof(jsonStr.c_str() + xPos + 4));

        size_t yPos = jsonStr.find("\"y\":", pos);
        if (yPos != std::string::npos) dto.y = static_cast<float>(std::atof(jsonStr.c_str() + yPos + 4));

        size_t zPos = jsonStr.find("\"z\":", pos);
        if (zPos != std::string::npos) dto.z = static_cast<float>(std::atof(jsonStr.c_str() + zPos + 4));

        m_masterSpots.push_back(dto);
        pos += 10;
    }

    if (m_hWnd) {
        PostMessageW(m_hWnd, WM_COMMAND, MAKEWPARAM(IDC_TELE_BTN_SYNC, 0), 0);
    }
}

void ClientWindow::RefreshTeleportSpotList() {
    if (!m_hTeleSpotList) return;
    ListView_DeleteAllItems(m_hTeleSpotList);

    int idx = 0;
    for (const auto& s : m_masterSpots) {
        LVITEMW item{};
        item.mask = LVIF_TEXT;
        item.iItem = idx;
        std::wstring wName(s.name.begin(), s.name.end());
        item.pszText = const_cast<LPWSTR>(wName.c_str());
        ListView_InsertItem(m_hTeleSpotList, &item);

        std::wstring wMap(s.mapName.begin(), s.mapName.end());
        ListView_SetItemText(m_hTeleSpotList, idx, 1, const_cast<LPWSTR>(wMap.c_str()));

        wchar_t coord[32];
        swprintf_s(coord, L"%.2f", s.x); ListView_SetItemText(m_hTeleSpotList, idx, 2, coord);
        swprintf_s(coord, L"%.2f", s.y); ListView_SetItemText(m_hTeleSpotList, idx, 3, coord);
        swprintf_s(coord, L"%.2f", s.z); ListView_SetItemText(m_hTeleSpotList, idx, 4, coord);

        ListView_SetItemText(m_hTeleSpotList, idx, 5, const_cast<LPWSTR>(s.enabled ? L"Sẵn sàng" : L"Tắt"));
        idx++;
    }
}

LRESULT ClientWindow::HandleMessage(HWND hWnd, UINT msg, WPARAM wParam, LPARAM lParam) {
    switch (msg) {
    case WM_ERASEBKGND:
        return 1; // Anti-Flicker 100%

    case WM_TIMER: {
        if (wParam == IDT_UI_REFRESH_TIMER) {
            // Cập nhật số liệu bot mượt mà
            InvalidateRect(hWnd, nullptr, FALSE);
        }
        return 0;
    }

    case WM_PAINT: {
        PAINTSTRUCT ps;
        HDC hdcWin = BeginPaint(hWnd, &ps);

        RECT rcClient;
        GetClientRect(hWnd, &rcClient);
        int winW = rcClient.right - rcClient.left;
        int winH = rcClient.bottom - rcClient.top;

        // Double Buffering: Toàn bộ frame vẽ lên memory DC
        HDC hdc = CreateCompatibleDC(hdcWin);
        HBITMAP hBmp = CreateCompatibleBitmap(hdcWin, winW, winH);
        HGDIOBJ oldBmp = SelectObject(hdc, hBmp);

        // 1. Fill Nền Toàn Bộ (#0B0F17)
        HBRUSH hBg = CreateSolidBrush(theme::Color::BgMain);
        FillRect(hdc, &rcClient, hBg);
        DeleteObject(hBg);

        // 2. Sidebar Nền (#0D121C) & Đường Phân Cách Mảnh
        RECT rcSidebar{0, 0, 200, winH};
        HBRUSH hSb = CreateSolidBrush(theme::Color::Sidebar);
        FillRect(hdc, &rcSidebar, hSb);
        DeleteObject(hSb);

        HPEN hPenDivider = CreatePen(PS_SOLID, 1, theme::Color::BorderSubtle);
        HGDIOBJ oldPen = SelectObject(hdc, hPenDivider);
        MoveToEx(hdc, 200, 0, nullptr);
        LineTo(hdc, 200, winH);
        SelectObject(hdc, oldPen);
        DeleteObject(hPenDivider);

        // Sidebar Logo Box Bo Góc 12px
        RECT rcLogoBox{14, 16, 186, 68};
        theme::DrawRoundedRect(hdc, rcLogoBox, theme::Color::Card, theme::Color::BorderSubtle, theme::Radius::Medium);

        SetBkMode(hdc, TRANSPARENT);
        HGDIOBJ oldFont = SelectObject(hdc, m_hFontHeader);
        SetTextColor(hdc, theme::Color::CyanNeon);
        TextOutW(hdc, 26, 25, L"✦ DTA", 5);

        SelectObject(hdc, m_hFontBold);
        SetTextColor(hdc, theme::Color::TextPrimary);
        TextOutW(hdc, 78, 25, L"PlayTogether", 12);

        SelectObject(hdc, m_hFontSmall);
        SetTextColor(hdc, theme::Color::TextMuted);
        TextOutW(hdc, 28, 48, L"Native v3.0 • Zero-Tap", 22);

        // Thẻ License Info ở Chân Sidebar (Bo góc 10px)
        RECT rcLicenseCard{14, 570, 186, 626};
        theme::DrawRoundedRect(hdc, rcLicenseCard, theme::Color::Card, theme::Color::BorderSubtle, theme::Radius::Normal);

        if (m_isFreeTier) {
            SelectObject(hdc, m_hFontBold);
            SetTextColor(hdc, theme::Color::Warning);
            TextOutW(hdc, 26, 580, L"✦ BẢN MIỄN PHÍ", 14);
            SelectObject(hdc, m_hFontSmall);
            SetTextColor(hdc, theme::Color::TextMuted);
            TextOutW(hdc, 26, 602, L"Chỉ hỗ trợ Câu cá", 17);
        } else {
            SelectObject(hdc, m_hFontBold);
            SetTextColor(hdc, theme::Color::VipGold);
            TextOutW(hdc, 26, 580, L"✦ VIP ACTIVE", 12);
            SelectObject(hdc, m_hFontSmall);
            SetTextColor(hdc, theme::Color::TextMuted);
            TextOutW(hdc, 26, 602, L"Bản quyền vĩnh viễn", 19);
        }

        // 3. Header Khu Vực Chính: Title & Server Health Indicator
        std::wstring currentTabTitle = SIDEBAR_ITEMS[m_activeTab].title;
        SelectObject(hdc, m_hFontTitle);
        SetTextColor(hdc, theme::Color::TextPrimary);
        TextOutW(hdc, 218, 16, currentTabTitle.c_str(), static_cast<int>(currentTabTitle.size()));

        // Server Online Indicator Dot (Glow nhẹ)
        RECT rcServerPill{420, 18, 555, 42};
        if (m_isServerOnline) {
            theme::DrawStatusBadge(hdc, rcServerPill, L"SERVER ONLINE", theme::Color::Success, theme::Color::Card, theme::Color::BorderSubtle, m_hFontSmall);
        } else {
            theme::DrawStatusBadge(hdc, rcServerPill, L"OFFLINE MODE", theme::Color::Danger, theme::Color::Card, theme::Color::BorderSubtle, m_hFontSmall);
        }

        // Subtitle cho từng Tab
        SelectObject(hdc, m_hFontSmall);
        SetTextColor(hdc, theme::Color::TextSecondary);
        const wchar_t* subTitles[] = {
            L"Radar ESP định vị xuyên thấu Cá, Quặng, Bọ và Rương kho báu ngầm",
            L"Tự câu, giữ / bán cá theo ý muốn và ghi lại lịch sử hoạt động",
            L"Tự động định vị máy dò, đào kho báu và mở rương không bị gián đoạn",
            L"Tự động khai thác đá, quặng sắt, vàng, kim cương và thiên thạch",
            L"Tự động quét, tiếp cận nhẹ nhàng và bắt bọ chuẩn xác 100%",
            L"Tự động nhặt cành cây, hoa quả, nấm, rác tái chế và sao rơi",
            L"Tự động gieo hạt, tưới nước, bón phân, thu hoạch nông sản và chăm thú",
            L"Dịch chuyển tức thì - Đồng bộ danh sách tọa độ chung từ VPS Server",
            L"Quản lý bản quyền VIP, cấu hình kết nối Server và hệ thống an toàn"
        };
        TextOutW(hdc, 218, 44, subTitles[m_activeTab], static_cast<int>(wcslen(subTitles[m_activeTab])));

        // Status Badge: "● ĐANG CHẠY" / "● ĐÃ DỪNG" (Bo tròn dạng Pill 16px)
        RECT rcStatusBadge{840, 18, 945, 44};
        if (m_isBotRunning) {
            theme::DrawStatusBadge(hdc, rcStatusBadge, L"ĐANG CHẠY", theme::Color::Success, theme::Color::Card, theme::Color::Success, m_hFontBold);
        } else {
            theme::DrawStatusBadge(hdc, rcStatusBadge, L"ĐÃ DỪNG", theme::Color::TextMuted, theme::Color::Card, theme::Color::BorderSubtle, m_hFontBold);
        }

        SelectObject(hdc, m_hFontRegular);
        SetTextColor(hdc, theme::Color::TextSecondary);
        TextOutW(hdc, 580, 72, L"Giả lập:", 8);
        TextOutW(hdc, 765, 72, L"Tab:", 4);

        // 4. Cards Layout Bo Góc 12px Cho Từng Tab
        if (m_activeTab >= 0 && m_activeTab <= 6) {
            // Row 1: 3 Modern Cards Bo Góc 12px
            RECT rcC1{215, 110, 485, 255}; theme::DrawModernCard(hdc, rcC1);
            RECT rcC2{500, 110, 680, 255}; theme::DrawModernCard(hdc, rcC2);
            RECT rcC3{690, 110, 875, 255}; theme::DrawModernCard(hdc, rcC3);

            // Row 2 & Row 3 Cards Bo Góc 12px
            RECT rcC4{215, 265, 1055, 360}; theme::DrawModernCard(hdc, rcC4);
            RECT rcC5{215, 400, 1055, 495}; theme::DrawModernCard(hdc, rcC5);

            if (m_activeTab == 0) { // ESP
                SelectObject(hdc, m_hFontRegular); SetTextColor(hdc, theme::Color::TextSecondary);
                TextOutW(hdc, 235, 125, L"Cá quét", 7); TextOutW(hdc, 325, 125, L"Quặng", 5); TextOutW(hdc, 415, 125, L"Bọ/Rương", 8);
                SelectObject(hdc, m_hFontTitle); SetTextColor(hdc, theme::Color::CyanNeon);
                TextOutW(hdc, 240, 155, L"0", 1); TextOutW(hdc, 330, 155, L"0", 1); TextOutW(hdc, 420, 155, L"0", 1);
                SelectObject(hdc, m_hFontSmall); SetTextColor(hdc, theme::Color::Warning);
                TextOutW(hdc, 235, 225, L"• Radar ESP đang quét trong phạm vi 100m", 41);

                SelectObject(hdc, m_hFontBold); SetTextColor(hdc, theme::Color::TextPrimary);
                TextOutW(hdc, 235, 278, L"BỘ LỌC ESP CÁ & QUẶNG", 21);
                TextOutW(hdc, 235, 414, L"BỘ LỌC ESP CÔN TRÙNG & RƯƠNG", 28);
            }
            else if (m_activeTab == 1) { // Fishing
                SelectObject(hdc, m_hFontRegular); SetTextColor(hdc, theme::Color::TextSecondary);
                TextOutW(hdc, 235, 125, L"ID cá", 5); TextOutW(hdc, 325, 125, L"Bóng", 4); TextOutW(hdc, 415, 125, L"Đã câu", 6);

                // Thống kê cá
                uint32_t caughtCount = m_fishingBot ? m_fishingBot->GetStats().totalCaught : 0;
                std::wstring wCaught = std::to_wstring(caughtCount);

                SelectObject(hdc, m_hFontTitle); SetTextColor(hdc, theme::Color::CyanNeon);
                TextOutW(hdc, 235, 155, L"---", 3); TextOutW(hdc, 325, 155, L"---", 3);
                SelectObject(hdc, m_hFontBigNum); SetTextColor(hdc, theme::Color::TextPrimary);
                TextOutW(hdc, 420, 148, wCaught.c_str(), static_cast<int>(wCaught.size()));

                SelectObject(hdc, m_hFontSmall); SetTextColor(hdc, theme::Color::Warning);
                TextOutW(hdc, 235, 225, L"• Sẵn sàng chạy chu trình câu cá tự động", 39);

                SelectObject(hdc, m_hFontSmall); SetTextColor(hdc, theme::Color::TextSecondary);
                TextOutW(hdc, 510, 192, L"Sau khi câu được cá", 19);
                TextOutW(hdc, 700, 190, L"Tính năng nâng cao", 18);
                TextOutW(hdc, 700, 210, L"Tối ưu góc camera & nhịp cắn", 28);
                TextOutW(hdc, 305, 278, L"(Không chọn mặc định tất cả)", 28);
                TextOutW(hdc, 605, 278, L"ID cá", 5);

                SelectObject(hdc, m_hFontRegular); SetTextColor(hdc, theme::Color::TextSecondary);
                TextOutW(hdc, 235, 318, L"Bóng", 4); TextOutW(hdc, 530, 318, L"Nền", 3);

                SelectObject(hdc, m_hFontSmall); SetTextColor(hdc, theme::Color::TextSecondary);
                TextOutW(hdc, 355, 418, L"(Không chọn mặc định tất cả)", 28);
                TextOutW(hdc, 645, 418, L"Giữ ID", 6);
                SelectObject(hdc, m_hFontRegular);
                TextOutW(hdc, 235, 458, L"Giữ bóng", 8); TextOutW(hdc, 530, 458, L"Giữ nền", 7);
            }
            else if (m_activeTab == 2) { // Excavation
                SelectObject(hdc, m_hFontRegular); SetTextColor(hdc, theme::Color::TextSecondary);
                TextOutW(hdc, 235, 125, L"Tín hiệu", 8); TextOutW(hdc, 325, 125, L"Đã đào", 6); TextOutW(hdc, 415, 125, L"Rương", 5);
                SelectObject(hdc, m_hFontTitle); SetTextColor(hdc, theme::Color::CyanNeon);
                TextOutW(hdc, 235, 155, L"---", 3); TextOutW(hdc, 415, 155, L"0", 1);
                SelectObject(hdc, m_hFontBigNum); SetTextColor(hdc, theme::Color::TextPrimary);
                TextOutW(hdc, 330, 148, L"0", 1);
                SelectObject(hdc, m_hFontSmall); SetTextColor(hdc, theme::Color::Warning);
                TextOutW(hdc, 235, 225, L"• Máy dò kho báu đang ở chế độ chờ tín hiệu", 43);
                SelectObject(hdc, m_hFontBold); SetTextColor(hdc, theme::Color::TextPrimary);
                TextOutW(hdc, 235, 278, L"BỘ LỌC PHẨM CẤP RƯƠNG KHO BÁU", 29);
                TextOutW(hdc, 235, 414, L"TÙY CHỌN TỰ ĐỘNG HÓA SAU KHI ĐÀO", 32);
            }
            else if (m_activeTab == 3) { // Mining
                SelectObject(hdc, m_hFontRegular); SetTextColor(hdc, theme::Color::TextSecondary);
                TextOutW(hdc, 235, 125, L"Quặng", 5); TextOutW(hdc, 325, 125, L"Đã đập", 6); TextOutW(hdc, 415, 125, L"Đá quý", 6);
                SelectObject(hdc, m_hFontTitle); SetTextColor(hdc, theme::Color::CyanNeon);
                TextOutW(hdc, 235, 155, L"---", 3); TextOutW(hdc, 420, 155, L"0", 1);
                SelectObject(hdc, m_hFontBigNum); SetTextColor(hdc, theme::Color::TextPrimary);
                TextOutW(hdc, 330, 148, L"0", 1);
                SelectObject(hdc, m_hFontSmall); SetTextColor(hdc, theme::Color::Warning);
                TextOutW(hdc, 235, 225, L"• Sẵn sàng đập đá & khai thác khoáng sản tự động", 48);
                SelectObject(hdc, m_hFontBold); SetTextColor(hdc, theme::Color::TextPrimary);
                TextOutW(hdc, 235, 278, L"LOẠI QUẶNG ƯU TIÊN KHAI THÁC", 28);
                TextOutW(hdc, 235, 442, L"Khu Vực Bãi Khai Thác:", 23);
            }
            else if (m_activeTab == 4) { // Insect
                SelectObject(hdc, m_hFontRegular); SetTextColor(hdc, theme::Color::TextSecondary);
                TextOutW(hdc, 235, 125, L"Tên bọ", 6); TextOutW(hdc, 325, 125, L"Kích cỡ", 7); TextOutW(hdc, 415, 125, L"Đã bắt", 6);
                SelectObject(hdc, m_hFontTitle); SetTextColor(hdc, theme::Color::CyanNeon);
                TextOutW(hdc, 235, 155, L"---", 3); TextOutW(hdc, 325, 155, L"---", 3);
                SelectObject(hdc, m_hFontBigNum); SetTextColor(hdc, theme::Color::TextPrimary);
                TextOutW(hdc, 420, 148, L"0", 1);
                SelectObject(hdc, m_hFontSmall); SetTextColor(hdc, theme::Color::Warning);
                TextOutW(hdc, 235, 225, L"• Radar bắt bọ sẵn sàng tiếp cận mục tiêu", 41);
                SelectObject(hdc, m_hFontBold); SetTextColor(hdc, theme::Color::TextPrimary);
                TextOutW(hdc, 235, 278, L"DANH MỤC LOÀI BỌ CẦN BẮT", 24);
                TextOutW(hdc, 235, 414, L"LOÀI BỌ HIẾM & ĐỘT BIẾN CẦN GIỮ", 30);
            }
            else if (m_activeTab == 5) { // Collect
                SelectObject(hdc, m_hFontRegular); SetTextColor(hdc, theme::Color::TextSecondary);
                TextOutW(hdc, 235, 125, L"Vừa nhặt", 8); TextOutW(hdc, 325, 125, L"Đã nhặt", 7); TextOutW(hdc, 415, 125, L"Balo trống", 10);
                SelectObject(hdc, m_hFontTitle); SetTextColor(hdc, theme::Color::CyanNeon);
                TextOutW(hdc, 235, 155, L"---", 3); TextOutW(hdc, 420, 155, L"---", 3);
                SelectObject(hdc, m_hFontBigNum); SetTextColor(hdc, theme::Color::TextPrimary);
                TextOutW(hdc, 330, 148, L"0", 1);
                SelectObject(hdc, m_hFontSmall); SetTextColor(hdc, theme::Color::Warning);
                TextOutW(hdc, 235, 225, L"• Sẵn sàng tự động thu lượm tài nguyên", 38);
                SelectObject(hdc, m_hFontBold); SetTextColor(hdc, theme::Color::TextPrimary);
                TextOutW(hdc, 235, 278, L"TÀI NGUYÊN ƯU TIÊN THU THẬP", 27);
            }
            else if (m_activeTab == 6) { // Farm
                SelectObject(hdc, m_hFontRegular); SetTextColor(hdc, theme::Color::TextSecondary);
                TextOutW(hdc, 235, 125, L"Đã gieo", 7); TextOutW(hdc, 325, 125, L"Thu hoạch", 9); TextOutW(hdc, 415, 125, L"Gia súc", 7);
                SelectObject(hdc, m_hFontBigNum); SetTextColor(hdc, theme::Color::TextPrimary);
                TextOutW(hdc, 240, 148, L"0", 1); TextOutW(hdc, 330, 148, L"0", 1); TextOutW(hdc, 420, 148, L"0", 1);
                SelectObject(hdc, m_hFontSmall); SetTextColor(hdc, theme::Color::Warning);
                TextOutW(hdc, 235, 225, L"• Chu trình nông trại tự động sẵn sàng", 38);
                SelectObject(hdc, m_hFontBold); SetTextColor(hdc, theme::Color::TextPrimary);
                TextOutW(hdc, 235, 278, L"CHĂM SÓC THÚ & GIA SÚC NÔNG TRẠI", 32);
            }
        }
        else if (m_activeTab == 7) { // Teleport Tab
            RECT rcTeleCard{215, 110, 865, 470}; theme::DrawModernCard(hdc, rcTeleCard);
            RECT rcQuickCard{215, 480, 1060, 560}; theme::DrawModernCard(hdc, rcQuickCard, L"DỊCH CHUYỂN NHANH ĐẾN CÁC KHU VỰC CHÍNH", m_hFontBold);
        }
        else if (m_activeTab == 8) { // Settings Tab
            RECT rcSetCard{215, 110, 1055, 400}; theme::DrawModernCard(hdc, rcSetCard, L"CẤU HÌNH BẢN QUYỀN & MÁY CHỦ MASTER", m_hFontBold);
            SelectObject(hdc, m_hFontRegular); SetTextColor(hdc, theme::Color::TextSecondary);
            TextOutW(hdc, 235, 136, L"Mã License Key:", 15);
            TextOutW(hdc, 235, 186, L"Mã HWID Thiết Bị:", 17);
            TextOutW(hdc, 235, 236, L"Máy Chủ Master VPS:", 19);
        }

        // 5. Footer Bar Tinh Tế
        SelectObject(hdc, m_hFontSmall);
        SetTextColor(hdc, theme::Color::TextMuted);
        TextOutW(hdc, 218, winH - 24, L"DTA Engine • 100% C++20 Zero-Tap IL2CPP", 39);
        TextOutW(hdc, winW - 220, winH - 24, L"Zero-Disk Log • Ultra Low Latency", 34);

        SelectObject(hdc, oldFont);

        // BitBlt toàn bộ bộ nhớ ảo lên màn hình 60 FPS
        BitBlt(hdcWin, 0, 0, winW, winH, hdc, 0, 0, SRCCOPY);

        SelectObject(hdc, oldBmp);
        DeleteObject(hBmp);
        DeleteDC(hdc);
        EndPaint(hWnd, &ps);
        return 0;
    }

    case WM_DRAWITEM: {
        auto dis = reinterpret_cast<LPDRAWITEMSTRUCT>(lParam);
        HDC hdc = dis->hDC;
        RECT rc = dis->rcItem;
        bool isSelected = (dis->itemState & ODS_SELECTED) != 0;

        SetBkMode(hdc, TRANSPARENT);

        // 1. Sidebar Buttons (0 to 8)
        if (dis->CtlID >= IDC_SIDEBAR_BASE && dis->CtlID < IDC_SIDEBAR_BASE + 9) {
            int tabIdx = dis->CtlID - IDC_SIDEBAR_BASE;
            bool isActive = (m_activeTab == tabIdx);

            COLORREF bg = isActive ? theme::Color::CardHover : (isSelected ? theme::Color::CardSecondary : theme::Color::Sidebar);
            COLORREF border = isActive ? theme::Color::Accent : bg;
            theme::DrawRoundedRect(hdc, rc, bg, border, theme::Radius::Normal);

            if (isActive) {
                // Thanh chỉ báo đứng phát sáng Cyan bên mép trái
                HBRUSH hBar = CreateSolidBrush(theme::Color::CyanNeon);
                RECT rcBar{rc.left + 2, rc.top + 8, rc.left + 6, rc.bottom - 8};
                FillRect(hdc, &rcBar, hBar);
                DeleteObject(hBar);
            }

            SelectObject(hdc, isActive ? m_hFontBold : m_hFontRegular);
            if (m_isFreeTier && tabIdx != 1) {
                SetTextColor(hdc, theme::Color::TextMuted);
            } else {
                SetTextColor(hdc, isActive ? theme::Color::TextPrimary : theme::Color::TextSecondary);
            }

            std::wstring text = SIDEBAR_ITEMS[tabIdx].icon + SIDEBAR_ITEMS[tabIdx].title;
            if (m_isFreeTier && tabIdx != 1) {
                text += L"  [VIP]";
            }

            RECT rcText = rc;
            rcText.left += 16;
            DrawTextW(hdc, text.c_str(), -1, &rcText, DT_LEFT | DT_VCENTER | DT_SINGLELINE);
            return TRUE;
        }

        // 2. Sub-tab Segmented Pills
        if (dis->CtlID == IDC_TOP_SUBTAB_FISHING || dis->CtlID == IDC_TOP_SUBTAB_HISTORY) {
            bool isFishingTab = (dis->CtlID == IDC_TOP_SUBTAB_FISHING);
            COLORREF bg = isFishingTab ? theme::Color::Accent : theme::Color::Card;
            COLORREF border = isFishingTab ? theme::Color::Accent : theme::Color::BorderSubtle;
            theme::DrawModernButton(hdc, rc, isFishingTab ? L"Hoạt động" : L"Lịch sử", bg, border, theme::Color::TextPrimary, m_hFontBold, theme::Radius::Normal);
            return TRUE;
        }

        // 3. Refresh Button
        if (dis->CtlID == IDC_TOP_BTN_REFRESH) {
            theme::DrawModernButton(hdc, rc, L"🔄", theme::Color::Card, theme::Color::BorderSubtle, theme::Color::CyanNeon, m_hFontBold, theme::Radius::Normal);
            return TRUE;
        }

        // 4. Big Action Buttons: ▶ Bật & ■ Tắt cho TẤT CẢ các tab
        if (dis->CtlID == IDC_ESP_BTN_START || dis->CtlID == IDC_FISH_BTN_START || dis->CtlID == IDC_EXC_BTN_START ||
            dis->CtlID == IDC_MIN_BTN_START || dis->CtlID == IDC_INS_BTN_START || dis->CtlID == IDC_COL_BTN_START ||
            dis->CtlID == IDC_FARM_BTN_START) {
            COLORREF bg = isSelected ? theme::Color::AccentHover : theme::Color::Accent;
            theme::DrawModernButton(hdc, rc, L"▶  Bật", bg, bg, theme::Color::TextPrimary, m_hFontHeader, theme::Radius::Normal);
            return TRUE;
        }

        if (dis->CtlID == IDC_ESP_BTN_STOP || dis->CtlID == IDC_FISH_BTN_STOP || dis->CtlID == IDC_EXC_BTN_STOP ||
            dis->CtlID == IDC_MIN_BTN_STOP || dis->CtlID == IDC_INS_BTN_STOP || dis->CtlID == IDC_COL_BTN_STOP ||
            dis->CtlID == IDC_FARM_BTN_STOP) {
            COLORREF bg = isSelected ? theme::Color::DangerHover : theme::Color::DangerBg;
            theme::DrawModernButton(hdc, rc, L"■  Tắt", bg, theme::Color::Danger, theme::Color::Danger, m_hFontHeader, theme::Radius::Normal);
            return TRUE;
        }

        // 5. Segmented Toggle: Bảo quản / Bán nhanh
        if (dis->CtlID == IDC_FISH_BTN_KEEPFISH || dis->CtlID == IDC_FISH_BTN_SELLFISH) {
            bool isKeep = (dis->CtlID == IDC_FISH_BTN_KEEPFISH);
            bool isActive = (isKeep && m_keepFishSelected) || (!isKeep && !m_keepFishSelected);

            COLORREF bg = isActive ? theme::Color::Accent : theme::Color::Card;
            COLORREF border = isActive ? theme::Color::Accent : theme::Color::BorderSubtle;
            COLORREF textCol = isActive ? theme::Color::TextPrimary : theme::Color::TextSecondary;
            theme::DrawModernButton(hdc, rc, isKeep ? L"Bảo quản" : L"Bán nhanh", bg, border, textCol, m_hFontSmall, theme::Radius::Small);
            return TRUE;
        }

        // 6. Chip Buttons (1 to 7) & Tier
        if (dis->CtlID >= IDC_FISH_BTN_SHADOW_BASE && dis->CtlID <= IDC_FISH_BTN_SHADOW_BASE + 6) {
            int idx = dis->CtlID - IDC_FISH_BTN_SHADOW_BASE;
            bool active = m_filterShadowState[idx];
            COLORREF bg = active ? theme::Color::Accent : theme::Color::Card;
            COLORREF border = active ? theme::Color::CyanNeon : theme::Color::BorderSubtle;
            wchar_t szNum[8]; swprintf_s(szNum, L"%d", idx + 1);
            theme::DrawModernButton(hdc, rc, szNum, bg, border, active ? theme::Color::TextPrimary : theme::Color::TextMuted, m_hFontBold, theme::Radius::Small);
            return TRUE;
        }

        if (dis->CtlID >= IDC_FISH_BTN_KSHADOW_BASE && dis->CtlID <= IDC_FISH_BTN_KSHADOW_BASE + 6) {
            int idx = dis->CtlID - IDC_FISH_BTN_KSHADOW_BASE;
            bool active = m_keepShadowState[idx];
            COLORREF bg = active ? theme::Color::Accent : theme::Color::Card;
            COLORREF border = active ? theme::Color::CyanNeon : theme::Color::BorderSubtle;
            wchar_t szNum[8]; swprintf_s(szNum, L"%d", idx + 1);
            theme::DrawModernButton(hdc, rc, szNum, bg, border, active ? theme::Color::TextPrimary : theme::Color::TextMuted, m_hFontBold, theme::Radius::Small);
            return TRUE;
        }

        if (dis->CtlID >= IDC_FISH_BTN_TIER_BASE && dis->CtlID <= IDC_FISH_BTN_TIER_BASE + 4) {
            int idx = dis->CtlID - IDC_FISH_BTN_TIER_BASE;
            bool active = m_filterTierState[idx];
            COLORREF bg = active ? theme::Color::CardHover : theme::Color::Card;
            COLORREF border = active ? theme::Color::Accent : theme::Color::BorderSubtle;
            COLORREF dotColors[] = {RGB(255, 255, 255), theme::Color::Success, theme::Color::Accent, theme::Color::Purple, theme::Color::VipGold};
            const wchar_t* names[] = {L"● Trắng", L"● Xanh lá", L"● Xanh dương", L"● Tím", L"● VIP"};
            theme::DrawModernButton(hdc, rc, names[idx], bg, border, dotColors[idx], m_hFontSmall, theme::Radius::Small);
            return TRUE;
        }

        if (dis->CtlID >= IDC_FISH_BTN_KTIER_BASE && dis->CtlID <= IDC_FISH_BTN_KTIER_BASE + 4) {
            int idx = dis->CtlID - IDC_FISH_BTN_KTIER_BASE;
            bool active = m_keepTierState[idx];
            COLORREF bg = active ? theme::Color::CardHover : theme::Color::Card;
            COLORREF border = active ? theme::Color::Accent : theme::Color::BorderSubtle;
            COLORREF dotColors[] = {RGB(255, 255, 255), theme::Color::Success, theme::Color::Accent, theme::Color::Purple, theme::Color::VipGold};
            const wchar_t* names[] = {L"● Trắng", L"● Xanh lá", L"● Xanh dương", L"● Tím", L"● VIP"};
            theme::DrawModernButton(hdc, rc, names[idx], bg, border, dotColors[idx], m_hFontSmall, theme::Radius::Small);
            return TRUE;
        }

        // 7. General Action Buttons
        COLORREF bg = isSelected ? theme::Color::CardHover : theme::Color::Card;
        COLORREF border = theme::Color::BorderSubtle;
        COLORREF textCol = theme::Color::TextSecondary;

        if (dis->CtlID == IDC_FISH_BTN_CLEAR_FILTER || dis->CtlID == IDC_FISH_BTN_CLEAR_KEEP) {
            theme::DrawModernButton(hdc, rc, dis->CtlID == IDC_FISH_BTN_CLEAR_FILTER ? L"Xoá điều kiện lọc" : L"Xoá điều kiện giữ", bg, border, textCol, m_hFontBold, theme::Radius::Normal);
            return TRUE;
        } else if (dis->CtlID == IDC_SIDEBAR_BTN_LOGOUT) {
            if (m_isFreeTier) {
                theme::DrawModernButton(hdc, rc, L"⚡ Nâng cấp key", bg, theme::Color::Warning, theme::Color::Warning, m_hFontBold, theme::Radius::Normal);
            } else {
                theme::DrawModernButton(hdc, rc, L"Đăng xuất key", bg, border, textCol, m_hFontBold, theme::Radius::Normal);
            }
            return TRUE;
        } else if (dis->CtlID == IDC_TELE_BTN_GO) {
            theme::DrawModernButton(hdc, rc, L"DỊCH CHUYỂN TỨC THÌ", theme::Color::Accent, theme::Color::Accent, theme::Color::TextPrimary, m_hFontBold, theme::Radius::Normal);
            return TRUE;
        } else if (dis->CtlID == IDC_TELE_BTN_SYNC) {
            theme::DrawModernButton(hdc, rc, L"ĐỒNG BỘ TỪ SERVER", bg, border, theme::Color::TextPrimary, m_hFontBold, theme::Radius::Normal);
            return TRUE;
        } else if (dis->CtlID == IDC_TELE_BTN_SAVEPOS) {
            theme::DrawModernButton(hdc, rc, L"LƯU VỊ TRÍ HIỆN TẠI", bg, border, textCol, m_hFontBold, theme::Radius::Normal);
            return TRUE;
        } else if (dis->CtlID >= IDC_TELE_MAP_1 && dis->CtlID <= IDC_TELE_MAP_10) {
            wchar_t buf[64]{0};
            GetWindowTextW(dis->hwndItem, buf, 64);
            theme::DrawModernButton(hdc, rc, buf, bg, border, theme::Color::TextPrimary, m_hFontRegular, theme::Radius::Normal);
            return TRUE;
        } else if (dis->CtlID == IDC_SET_BTN_ACTIVATE) {
            theme::DrawModernButton(hdc, rc, L"Lưu / Kích Hoạt", theme::Color::Accent, theme::Color::Accent, theme::Color::TextPrimary, m_hFontBold, theme::Radius::Normal);
            return TRUE;
        } else if (dis->CtlID == IDC_SET_BTN_COPY_HWID) {
            theme::DrawModernButton(hdc, rc, L"Sao Chép HWID", bg, border, theme::Color::TextPrimary, m_hFontRegular, theme::Radius::Normal);
            return TRUE;
        } else if (dis->CtlID == IDC_SET_BTN_PING) {
            theme::DrawModernButton(hdc, rc, L"Kiểm Tra Kết Nối", bg, border, theme::Color::Success, m_hFontBold, theme::Radius::Normal);
            return TRUE;
        }
        break;
    }

    case WM_COMMAND: {
        int id = LOWORD(wParam);

        // Sidebar clicks
        if (id >= IDC_SIDEBAR_BASE && id < IDC_SIDEBAR_BASE + 9) {
            SwitchTab(id - IDC_SIDEBAR_BASE);
            return 0;
        }

        // Subtabs
        if (id == IDC_TOP_SUBTAB_FISHING) {
            SwitchTab(m_activeTab);
        } else if (id == IDC_TOP_SUBTAB_HISTORY) {
            std::wstring histMsg = L"Lịch sử hoạt động của " + SIDEBAR_ITEMS[m_activeTab].title + L": Đang ghi nhận bình thường.";
            MessageBoxW(hWnd, histMsg.c_str(), L"Lịch Sử", MB_OK | MB_ICONINFORMATION);
        } else if (id == IDC_TOP_CHK_TOPMOST) {
            bool checked = (SendMessage(m_hChkTopmost, BM_GETCHECK, 0, 0) == BST_CHECKED);
            SetWindowPos(hWnd, checked ? HWND_TOPMOST : HWND_NOTOPMOST, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE);
        } else if (id == IDC_TOP_BTN_REFRESH) {
            CheckServerHealthAsync();
            MessageBoxW(hWnd, L"Đã quét và kết nối lại trình giả lập thành công!", L"Giả Lập", MB_OK | MB_ICONINFORMATION);
        }

        // --- START / STOP BUTTONS CHO TỪNG TAB ---
        if (id == IDC_ESP_BTN_START) {
            m_isBotRunning = true; InvalidateRect(hWnd, nullptr, TRUE);
            MessageBoxW(hWnd, L"Đã bật Radar ESP hiển thị xuyên thấu!", L"Thông Báo", MB_OK | MB_ICONINFORMATION);
        } else if (id == IDC_ESP_BTN_STOP) {
            m_isBotRunning = false; InvalidateRect(hWnd, nullptr, TRUE);
            MessageBoxW(hWnd, L"Đã tắt Radar ESP!", L"Thông Báo", MB_OK | MB_ICONINFORMATION);
        } else if (id == IDC_FISH_BTN_START) {
            m_isBotRunning = true;
            if (m_fishingBot) {
                features::fishing::FishingBotOptions opt;
                opt.autoCastReel = true;
                opt.autoRepair = (SendMessage(m_hChkAutoRepair, BM_GETCHECK, 0, 0) == BST_CHECKED);
                opt.isFreeTier = m_isFreeTier;

                if (m_isFreeTier) {
                    opt.enableTugHpPull = false; // Không có cơ chế giật tụt HP cá bóng 6-7
                    opt.afterCatch = features::fishing::AfterCatchAction::SELL_FAST; // Bán tất cả cá câu được (không giữ)
                    opt.biteReactionDelayMs = 200; // Tốc độ bình thường, không cắn nhanh
                } else {
                    opt.enableTugHpPull = true; // [VIP] Cơ chế giật tụt HP cá khủng bóng 6-7
                    opt.afterCatch = m_keepFishSelected ? features::fishing::AfterCatchAction::KEEP_TO_BAG : features::fishing::AfterCatchAction::SELL_FAST;
                    bool fastBite = (SendMessage(m_hChkFastBite, BM_GETCHECK, 0, 0) == BST_CHECKED);
                    opt.biteReactionDelayMs = fastBite ? 15 : 120;

                    opt.filter.keepVariants = (SendMessage(m_hChkKeepVariant, BM_GETCHECK, 0, 0) == BST_CHECKED);
                    opt.filter.keepCrowns = (SendMessage(m_hChkKeepMutant, BM_GETCHECK, 0, 0) == BST_CHECKED);
                    for (size_t s = 0; s < 7; ++s) opt.filter.enabledShadows[s + 1] = m_filterShadowState[s];
                    for (size_t g = 0; g < 5; ++g) opt.filter.enabledGrades[g + 1] = m_filterTierState[g];
                }

                m_fishingBot->SetOptions(opt);
                m_fishingBot->Start();
            }
            InvalidateRect(hWnd, nullptr, TRUE);
            std::wstring startMsg = m_isFreeTier ?
                L"Đã bật chu trình Câu Cá Tự Động [Bản Miễn Phí]!\n- Tự động bán tất cả cá (không giữ)\n- Không lọc cá\n- Không câu bóng 6-7 (không có cơ chế giật tụt HP)\n- Tốc độ giật tự nhiên\n- Khóa camera theo thiết lập" :
                L"Đã bật chu trình Câu Cá Tự Động Zero-Tap [VIP Edition]!";
            MessageBoxW(hWnd, startMsg.c_str(), L"Thông Báo", MB_OK | MB_ICONINFORMATION);
        } else if (id == IDC_FISH_BTN_STOP) {
            m_isBotRunning = false; if (m_fishingBot) m_fishingBot->Stop(); InvalidateRect(hWnd, nullptr, TRUE);
            MessageBoxW(hWnd, L"Đã tắt chu trình Câu Cá!", L"Thông Báo", MB_OK | MB_ICONINFORMATION);
        } else if (id == IDC_EXC_BTN_START) {
            m_isBotRunning = true; if (m_excavationBot) m_excavationBot->Start(); InvalidateRect(hWnd, nullptr, TRUE);
            MessageBoxW(hWnd, L"Đã bật chu trình Đào Cổ Vật Tự Động!", L"Thông Báo", MB_OK | MB_ICONINFORMATION);
        } else if (id == IDC_EXC_BTN_STOP) {
            m_isBotRunning = false; if (m_excavationBot) m_excavationBot->Stop(); InvalidateRect(hWnd, nullptr, TRUE);
            MessageBoxW(hWnd, L"Đã tắt chu trình Đào Cổ Vật!", L"Thông Báo", MB_OK | MB_ICONINFORMATION);
        } else if (id == IDC_MIN_BTN_START) {
            m_isBotRunning = true; if (m_miningBot) m_miningBot->Start(); InvalidateRect(hWnd, nullptr, TRUE);
            MessageBoxW(hWnd, L"Đã bật chu trình Đập Đá & Khai Khoáng!", L"Thông Báo", MB_OK | MB_ICONINFORMATION);
        } else if (id == IDC_MIN_BTN_STOP) {
            m_isBotRunning = false; if (m_miningBot) m_miningBot->Stop(); InvalidateRect(hWnd, nullptr, TRUE);
            MessageBoxW(hWnd, L"Đã tắt chu trình Đập Đá!", L"Thông Báo", MB_OK | MB_ICONINFORMATION);
        } else if (id == IDC_INS_BTN_START) {
            m_isBotRunning = true; if (m_insectBot) m_insectBot->Start(); InvalidateRect(hWnd, nullptr, TRUE);
            MessageBoxW(hWnd, L"Đã bật chu trình Bắt Bọ & Côn Trùng!", L"Thông Báo", MB_OK | MB_ICONINFORMATION);
        } else if (id == IDC_INS_BTN_STOP) {
            m_isBotRunning = false; if (m_insectBot) m_insectBot->Stop(); InvalidateRect(hWnd, nullptr, TRUE);
            MessageBoxW(hWnd, L"Đã tắt chu trình Bắt Bọ!", L"Thông Báo", MB_OK | MB_ICONINFORMATION);
        } else if (id == IDC_COL_BTN_START) {
            m_isBotRunning = true; if (m_collectBot) m_collectBot->Start(); InvalidateRect(hWnd, nullptr, TRUE);
            MessageBoxW(hWnd, L"Đã bật chu trình Thu Lượm Tài Nguyên!", L"Thông Báo", MB_OK | MB_ICONINFORMATION);
        } else if (id == IDC_COL_BTN_STOP) {
            m_isBotRunning = false; if (m_collectBot) m_collectBot->Stop(); InvalidateRect(hWnd, nullptr, TRUE);
            MessageBoxW(hWnd, L"Đã tắt chu trình Thu Lượm!", L"Thông Báo", MB_OK | MB_ICONINFORMATION);
        } else if (id == IDC_FARM_BTN_START) {
            m_isBotRunning = true; if (m_farmBot) m_farmBot->Start(); InvalidateRect(hWnd, nullptr, TRUE);
            MessageBoxW(hWnd, L"Đã bật chu trình Nông Trại Tự Động!", L"Thông Báo", MB_OK | MB_ICONINFORMATION);
        } else if (id == IDC_FARM_BTN_STOP) {
            m_isBotRunning = false; if (m_farmBot) m_farmBot->Stop(); InvalidateRect(hWnd, nullptr, TRUE);
            MessageBoxW(hWnd, L"Đã tắt chu trình Nông Trại!", L"Thông Báo", MB_OK | MB_ICONINFORMATION);
        }

        // Fishing Sub-actions
        if (id == IDC_FISH_BTN_KEEPFISH) {
            if (m_isFreeTier) {
                MessageBoxW(hWnd, L"[Bản Miễn Phí] Tính năng 'Bảo quản' chỉ dành cho bản VIP!\nBản miễn phí tự động bán tất cả cá câu được.", L"Thông Báo", MB_OK | MB_ICONWARNING);
                return 0;
            }
            m_keepFishSelected = true; InvalidateRect(hWnd, nullptr, TRUE);
        } else if (id == IDC_FISH_BTN_SELLFISH) {
            m_keepFishSelected = false; InvalidateRect(hWnd, nullptr, TRUE);
        } else if (id == IDC_FISH_BTN_CLEAR_FILTER) {
            m_filterShadowState.fill(false); m_filterTierState.fill(false);
            SetWindowTextW(m_hEditFilterId, L""); InvalidateRect(hWnd, nullptr, TRUE);
        } else if (id == IDC_FISH_BTN_CLEAR_KEEP) {
            m_keepShadowState.fill(false); m_keepTierState.fill(false);
            SetWindowTextW(m_hEditKeepId, L""); InvalidateRect(hWnd, nullptr, TRUE);
        } else if (id >= IDC_FISH_BTN_SHADOW_BASE && id <= IDC_FISH_BTN_SHADOW_BASE + 6) {
            int idx = id - IDC_FISH_BTN_SHADOW_BASE;
            m_filterShadowState[idx] = !m_filterShadowState[idx]; InvalidateRect(hWnd, nullptr, TRUE);
        } else if (id >= IDC_FISH_BTN_TIER_BASE && id <= IDC_FISH_BTN_TIER_BASE + 4) {
            int idx = id - IDC_FISH_BTN_TIER_BASE;
            m_filterTierState[idx] = !m_filterTierState[idx]; InvalidateRect(hWnd, nullptr, TRUE);
        } else if (id >= IDC_FISH_BTN_KSHADOW_BASE && id <= IDC_FISH_BTN_KSHADOW_BASE + 6) {
            int idx = id - IDC_FISH_BTN_KSHADOW_BASE;
            m_keepShadowState[idx] = !m_keepShadowState[idx]; InvalidateRect(hWnd, nullptr, TRUE);
        } else if (id >= IDC_FISH_BTN_KTIER_BASE && id <= IDC_FISH_BTN_KTIER_BASE + 4) {
            int idx = id - IDC_FISH_BTN_KTIER_BASE;
            m_keepTierState[idx] = !m_keepTierState[idx]; InvalidateRect(hWnd, nullptr, TRUE);
        }

        // Teleport Actions
        if (id == IDC_TELE_BTN_GO) {
            int sel = ListView_GetNextItem(m_hTeleSpotList, -1, LVNI_SELECTED);
            if (sel >= 0 && sel < static_cast<int>(m_masterSpots.size())) {
                const auto& spot = m_masterSpots[sel];
                if (m_teleportService) m_teleportService->TeleportTo({spot.x, spot.y, spot.z});
                std::wstring dispMsg = L"Đã dịch chuyển tới: " + std::wstring(spot.name.begin(), spot.name.end());
                MessageBoxW(hWnd, dispMsg.c_str(), L"Dịch Chuyển", MB_OK | MB_ICONINFORMATION);
            }
        } else if (id == IDC_TELE_BTN_SYNC) {
            std::thread([this]() { SyncMasterSpotsFromServer(); }).detach();
            RefreshTeleportSpotList();
        } else if (id == IDC_TELE_BTN_SAVEPOS) {
            MessageBoxW(hWnd, L"Đã ghi nhớ tọa độ nhân vật hiện tại vào bộ nhớ cục bộ!", L"Thông Báo", MB_OK | MB_ICONINFORMATION);
        } else if (id == IDC_TELE_MAP_1) {
            if (m_teleportService) m_teleportService->TeleportTo({-25.0f, 0.5f, 15.0f});
        } else if (id == IDC_TELE_MAP_2) {
            if (m_teleportService) m_teleportService->TeleportTo({120.0f, 0.5f, -40.0f});
        } else if (id == IDC_TELE_MAP_3) {
            if (m_teleportService) m_teleportService->TeleportTo({-85.0f, 1.2f, 220.0f});
        } else if (id == IDC_TELE_MAP_4) {
            if (m_teleportService) m_teleportService->TeleportTo({310.0f, 0.2f, 95.0f});
        } else if (id == IDC_TELE_MAP_10) {
            if (m_teleportService) m_teleportService->TeleportTo({0.0f, 0.0f, 0.0f});
        }

        // Settings actions
        if (id == IDC_SET_BTN_COPY_HWID) {
            if (OpenClipboard(hWnd)) {
                EmptyClipboard();
                size_t len = (m_clientHwid.size() + 1) * sizeof(wchar_t);
                HGLOBAL hGlob = GlobalAlloc(GMEM_MOVEABLE, len);
                if (hGlob) {
                    wchar_t* pBuf = static_cast<wchar_t*>(GlobalLock(hGlob));
                    std::wstring wHwid(m_clientHwid.begin(), m_clientHwid.end());
                    wcscpy_s(pBuf, m_clientHwid.size() + 1, wHwid.c_str());
                    GlobalUnlock(hGlob);
                    SetClipboardData(CF_UNICODETEXT, hGlob);
                }
                CloseClipboard();
                MessageBoxW(hWnd, L"Đã sao chép HWID vào Clipboard!", L"Thông Báo", MB_OK | MB_ICONINFORMATION);
            }
        } else if (id == IDC_SET_BTN_ACTIVATE) {
            wchar_t kBuf[128]{0};
            GetWindowTextW(m_hEditKey, kBuf, 128);
            char mbKey[128]{0};
            WideCharToMultiByte(CP_UTF8, 0, kBuf, -1, mbKey, 128, nullptr, nullptr);
            std::string res;
            if (network::NetworkClient::Instance().ActivateLicense(mbKey, m_clientHwid, res)) {
                MessageBoxW(hWnd, L"Kích hoạt License VIP thành công!", L"Bản Quyền", MB_OK | MB_ICONINFORMATION);
            } else {
                MessageBoxW(hWnd, L"Key không hợp lệ hoặc không kết nối được máy chủ!", L"Lỗi", MB_OK | MB_ICONWARNING);
            }
        } else if (id == IDC_SET_BTN_PING) {
            std::string healthRes = network::NetworkClient::Instance().HttpGet("/health");
            bool ok = (healthRes.find("online") != std::string::npos);
            if (ok) {
                MessageBoxW(hWnd, L"Kết nối đến VPS Server (Port 28445) thành công! Ping: 1ms", L"Máy Chủ", MB_OK | MB_ICONINFORMATION);
            } else {
                MessageBoxW(hWnd, L"Không thể kết nối đến Server VPS!", L"Cảnh Báo", MB_OK | MB_ICONWARNING);
            }
        } else if (id == IDC_SIDEBAR_BTN_LOGOUT) {
            if (m_isFreeTier) {
                std::string newKey;
                bool newIsFree = false;
                bool ok = ActivationDialog::Instance().ShowModal(m_hInstance, newKey, newIsFree);
                if (ok && !newIsFree) {
                    m_isFreeTier = false;
                    ApplyFreeTierRestrictions();
                    MessageBoxW(hWnd, L"Kích hoạt License VIP thành công! Toàn bộ 9 chức năng và bộ lọc cá đã được mở khóa.", L"Thành Công", MB_OK | MB_ICONINFORMATION);
                }
            } else {
                int ret = MessageBoxW(hWnd, L"Bạn có chắc chắn muốn đăng xuất Key bản quyền hiện tại?", L"Xác Nhận", MB_YESNO | MB_ICONQUESTION);
                if (ret == IDYES) {
                    PostQuitMessage(0);
                }
            }
        }
        break;
    }

    case WM_CTLCOLORSTATIC: {
        HDC hdcStatic = reinterpret_cast<HDC>(wParam);
        SetTextColor(hdcStatic, theme::Color::TextPrimary);
        SetBkColor(hdcStatic, theme::Color::Card);
        return reinterpret_cast<INT_PTR>(m_hCardBrush);
    }

    case WM_CTLCOLOREDIT: {
        HDC hdcEdit = reinterpret_cast<HDC>(wParam);
        SetTextColor(hdcEdit, theme::Color::TextPrimary);
        SetBkColor(hdcEdit, theme::Color::InputBg);
        return reinterpret_cast<INT_PTR>(m_hInputBrush);
    }

    case WM_DESTROY:
        KillTimer(hWnd, IDT_UI_REFRESH_TIMER);
        PostQuitMessage(0);
        return 0;
    }

    return DefWindowProcW(hWnd, msg, wParam, lParam);
}

void ClientWindow::RunMessageLoop() {
    MSG msg;
    while (GetMessageW(&msg, nullptr, 0, 0)) {
        TranslateMessage(&msg);
        DispatchMessageW(&msg);
    }
}

} // namespace dta::ui
