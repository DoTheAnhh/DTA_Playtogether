#include "ActivationDialog.hpp"
#include "client/network/NetworkClient.hpp"
#include "client/security/HWIDProvider.hpp"
#include "client/core/logger/AsyncLogger.hpp"
#include "shared/Theme.hpp"
#include <dwmapi.h>
#include <fstream>
#include <sstream>

#pragma comment(lib, "dwmapi.lib")

namespace dta::ui {

#define IDC_ACT_EDIT_KEY      3001
#define IDC_ACT_BTN_ACTIVATE  3002
#define IDC_ACT_BTN_EXIT      3003
#define IDC_ACT_BTN_FREE      3004

ActivationDialog& ActivationDialog::Instance() {
    static ActivationDialog instance;
    return instance;
}

ActivationDialog::ActivationDialog() {
    m_hBgBrush = CreateSolidBrush(theme::Color::BgMain);
    m_hCardBrush = CreateSolidBrush(theme::Color::Card);

    m_hFontTitle = CreateFontW(22, 0, 0, 0, FW_BOLD, FALSE, FALSE, FALSE,
        DEFAULT_CHARSET, OUT_DEFAULT_PRECIS, CLIP_DEFAULT_PRECIS, CLEARTYPE_QUALITY,
        DEFAULT_PITCH | FF_DONTCARE, L"Segoe UI");

    m_hFontRegular = CreateFontW(14, 0, 0, 0, FW_NORMAL, FALSE, FALSE, FALSE,
        DEFAULT_CHARSET, OUT_DEFAULT_PRECIS, CLIP_DEFAULT_PRECIS, CLEARTYPE_QUALITY,
        DEFAULT_PITCH | FF_DONTCARE, L"Segoe UI");

    m_hFontBold = CreateFontW(14, 0, 0, 0, FW_BOLD, FALSE, FALSE, FALSE,
        DEFAULT_CHARSET, OUT_DEFAULT_PRECIS, CLIP_DEFAULT_PRECIS, CLEARTYPE_QUALITY,
        DEFAULT_PITCH | FF_DONTCARE, L"Segoe UI");

    m_hFontMono = CreateFontW(14, 0, 0, 0, FW_NORMAL, FALSE, FALSE, FALSE,
        DEFAULT_CHARSET, OUT_DEFAULT_PRECIS, CLIP_DEFAULT_PRECIS, CLEARTYPE_QUALITY,
        FIXED_PITCH | FF_MODERN, L"Consolas");
}

ActivationDialog::~ActivationDialog() {
    if (m_hBgBrush) DeleteObject(m_hBgBrush);
    if (m_hCardBrush) DeleteObject(m_hCardBrush);
    if (m_hFontTitle) DeleteObject(m_hFontTitle);
    if (m_hFontRegular) DeleteObject(m_hFontRegular);
    if (m_hFontBold) DeleteObject(m_hFontBold);
    if (m_hFontMono) DeleteObject(m_hFontMono);
}

std::string ActivationDialog::LoadSavedKey() {
    try {
        std::ifstream file("data/app.json");
        if (file.is_open()) {
            std::stringstream buffer;
            buffer << file.rdbuf();
            std::string content = buffer.str();
            size_t pos = content.find("\"licenseKey\": \"");
            if (pos != std::string::npos) {
                size_t start = pos + 15;
                size_t end = content.find("\"", start);
                if (end != std::string::npos) {
                    return content.substr(start, end - start);
                }
            }
        }
    } catch (...) {}
    return "DTA-VIP-2026-KEY";
}

void ActivationDialog::SaveKey(const std::string& key) {
    try {
        std::ofstream file("data/app.json");
        if (file.is_open()) {
            file << "{\n  \"licenseKey\": \"" << key << "\",\n  \"page\": \"Dashboard\"\n}\n";
        }
    } catch (...) {}
}

LRESULT CALLBACK ActivationDialog::WndProc(HWND hWnd, UINT msg, WPARAM wParam, LPARAM lParam) {
    ActivationDialog* pThis = nullptr;
    if (msg == WM_NCCREATE) {
        auto cs = reinterpret_cast<CREATESTRUCT*>(lParam);
        pThis = reinterpret_cast<ActivationDialog*>(cs->lpCreateParams);
        SetWindowLongPtr(hWnd, GWLP_USERDATA, reinterpret_cast<LONG_PTR>(pThis));
        pThis->m_hWnd = hWnd;
    } else {
        pThis = reinterpret_cast<ActivationDialog*>(GetWindowLongPtr(hWnd, GWLP_USERDATA));
    }

    if (pThis) {
        return pThis->HandleMessage(hWnd, msg, wParam, lParam);
    }
    return DefWindowProcW(hWnd, msg, wParam, lParam);
}

bool ActivationDialog::ShowModal(HINSTANCE hInstance, std::string& outLicenseKey, bool& outIsFreeTier) {
    m_hInstance = hInstance;
    m_success = false;
    m_isFreeTier = false;

    WNDCLASSEXW wc{};
    wc.cbSize = sizeof(WNDCLASSEXW);
    wc.style = CS_HREDRAW | CS_VREDRAW;
    wc.lpfnWndProc = WndProc;
    wc.hInstance = hInstance;
    wc.hCursor = LoadCursor(nullptr, IDC_ARROW);
    wc.hbrBackground = m_hBgBrush;
    wc.lpszClassName = L"DTAActivationDialogClass";

    RegisterClassExW(&wc);

    int winW = 540;
    int winH = 430;
    int posX = (GetSystemMetrics(SM_CXSCREEN) - winW) / 2;
    int posY = (GetSystemMetrics(SM_CYSCREEN) - winH) / 2;

    m_hWnd = CreateWindowExW(
        WS_EX_DLGMODALFRAME | WS_EX_TOPMOST,
        L"DTAActivationDialogClass",
        L"DTA PlayTogether • Xác Thực Bản Quyền",
        WS_POPUP | WS_CAPTION | WS_SYSMENU,
        posX, posY, winW, winH,
        nullptr, nullptr, hInstance, this
    );

    if (!m_hWnd) return false;

    // Enable Windows 11 Dark Mode and Rounded Window Corners (DWMWA_WINDOW_CORNER_PREFERENCE = 33)
    BOOL dark = TRUE;
    DwmSetWindowAttribute(m_hWnd, 20, &dark, sizeof(dark));
    int cornerPref = 2; // DWMWCP_ROUND (Round Corners)
    DwmSetWindowAttribute(m_hWnd, 33, &cornerPref, sizeof(cornerPref));

    // Input Key Box (Centered, Sleek)
    std::string savedKey = LoadSavedKey();
    std::wstring wSavedKey(savedKey.begin(), savedKey.end());
    m_hEditKey = CreateWindowExW(WS_EX_CLIENTEDGE, L"EDIT", wSavedKey.c_str(),
        WS_CHILD | WS_VISIBLE | ES_AUTOHSCROLL,
        30, 132, 480, 36, m_hWnd, reinterpret_cast<HMENU>(IDC_ACT_EDIT_KEY), m_hInstance, nullptr);
    SendMessage(m_hEditKey, WM_SETFONT, reinterpret_cast<WPARAM>(m_hFontMono), TRUE);

    // Primary Action Buttons (Bo góc 10px qua Owner-Drawn)
    m_hBtnActivate = CreateWindowExW(0, L"BUTTON", L"XÁC NHẬN VIP",
        WS_CHILD | WS_VISIBLE | BS_OWNERDRAW,
        30, 180, 340, 44, m_hWnd, reinterpret_cast<HMENU>(IDC_ACT_BTN_ACTIVATE), m_hInstance, nullptr);

    m_hBtnExit = CreateWindowExW(0, L"BUTTON", L"THOÁT",
        WS_CHILD | WS_VISIBLE | BS_OWNERDRAW,
        380, 180, 130, 44, m_hWnd, reinterpret_cast<HMENU>(IDC_ACT_BTN_EXIT), m_hInstance, nullptr);

    // Free Tier Button (Emerald Pill)
    m_hBtnFreeTier = CreateWindowExW(0, L"BUTTON", L"🎁  DÙNG BẢN MIỄN PHÍ",
        WS_CHILD | WS_VISIBLE | BS_OWNERDRAW,
        30, 285, 480, 46, m_hWnd, reinterpret_cast<HMENU>(IDC_ACT_BTN_FREE), m_hInstance, nullptr);

    m_statusMessage = L"Vui lòng nhập License Key VIP hoặc chọn Dùng bản miễn phí.";
    m_statusColor = theme::Color::TextSecondary;

    ShowWindow(m_hWnd, SW_SHOW);
    UpdateWindow(m_hWnd);

    // Modal message loop
    MSG msg;
    while (GetMessageW(&msg, nullptr, 0, 0)) {
        TranslateMessage(&msg);
        DispatchMessageW(&msg);
        if (!IsWindow(m_hWnd)) break;
    }

    if (m_success) {
        outLicenseKey = m_activatedKey;
        outIsFreeTier = m_isFreeTier;
        return true;
    }
    return false;
}

LRESULT ActivationDialog::HandleMessage(HWND hWnd, UINT msg, WPARAM wParam, LPARAM lParam) {
    switch (msg) {
    case WM_ERASEBKGND:
        return 1; // Ngăn chặn flicker

    case WM_PAINT: {
        PAINTSTRUCT ps;
        HDC hdcWin = BeginPaint(hWnd, &ps);

        RECT rcClient;
        GetClientRect(hWnd, &rcClient);
        int w = rcClient.right - rcClient.left;
        int h = rcClient.bottom - rcClient.top;

        // Double Buffering: Tạo memory DC chống lag và flicker
        HDC hdc = CreateCompatibleDC(hdcWin);
        HBITMAP hBmp = CreateCompatibleBitmap(hdcWin, w, h);
        HGDIOBJ oldBmp = SelectObject(hdc, hBmp);

        // 1. Fill Background (#0B0F17)
        HBRUSH hBg = CreateSolidBrush(theme::Color::BgMain);
        FillRect(hdc, &rcClient, hBg);
        DeleteObject(hBg);

        // 2. Header Area: Logo & Subtitle
        SetBkMode(hdc, TRANSPARENT);
        HGDIOBJ oldFont = SelectObject(hdc, m_hFontTitle);
        SetTextColor(hdc, theme::Color::CyanNeon);
        TextOutW(hdc, 30, 22, L"✦ DTA PLAYTOGETHER", 18);

        SelectObject(hdc, m_hFontRegular);
        SetTextColor(hdc, theme::Color::TextSecondary);
        TextOutW(hdc, 30, 52, L"Kích Hoạt Bản Quyền VIP hoặc Trải Nghiệm Bản Miễn Phí", 53);

        // Đường accent mỏng dưới header
        HPEN hPenLine = CreatePen(PS_SOLID, 1, theme::Color::BorderSubtle);
        HGDIOBJ oldPen = SelectObject(hdc, hPenLine);
        MoveToEx(hdc, 30, 80, nullptr);
        LineTo(hdc, w - 30, 80);

        // 3. Label License Key
        SelectObject(hdc, m_hFontBold);
        SetTextColor(hdc, theme::Color::TextPrimary);
        TextOutW(hdc, 30, 105, L"NHẬP MÃ LICENSE KEY VIP:", 24);

        // 4. Status Message Text (Giữa 2 cụm nút)
        SelectObject(hdc, m_hFontBold);
        SetTextColor(hdc, m_statusColor);
        RECT rcStatus{30, 230, w - 30, 255};
        DrawTextW(hdc, m_statusMessage.c_str(), -1, &rcStatus, DT_LEFT | DT_VCENTER | DT_SINGLELINE);

        // 5. Divider Line trước Free Tier
        MoveToEx(hdc, 30, 265, nullptr);
        LineTo(hdc, w - 30, 265);
        SelectObject(hdc, oldPen);
        DeleteObject(hPenLine);

        // 6. Subtitle Note bên dưới Free Tier button
        SelectObject(hdc, m_hFontRegular);
        SetTextColor(hdc, theme::Color::TextMuted);
        RECT rcNote{30, 345, w - 30, 395};
        DrawTextW(hdc,
            L"* Bản miễn phí: Chỉ hỗ trợ Câu cá (bán toàn bộ cá, không lọc, không câu cá bóng 6-7, có khóa cam).",
            -1, &rcNote, DT_LEFT | DT_WORDBREAK);

        SelectObject(hdc, oldFont);

        // BitBlt toàn bộ bộ nhớ ảo lên màn hình 60 FPS
        BitBlt(hdcWin, 0, 0, w, h, hdc, 0, 0, SRCCOPY);

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

        if (dis->CtlID == IDC_ACT_BTN_ACTIVATE) {
            COLORREF bg = isSelected ? theme::Color::AccentHover : theme::Color::Accent;
            theme::DrawModernButton(hdc, rc, L"XÁC NHẬN VIP", bg, bg, theme::Color::TextPrimary, m_hFontBold, theme::Radius::Normal);
            return TRUE;
        } else if (dis->CtlID == IDC_ACT_BTN_EXIT) {
            COLORREF bg = isSelected ? theme::Color::CardHover : theme::Color::Card;
            theme::DrawModernButton(hdc, rc, L"THOÁT", bg, theme::Color::BorderSubtle, theme::Color::TextSecondary, m_hFontBold, theme::Radius::Normal);
            return TRUE;
        } else if (dis->CtlID == IDC_ACT_BTN_FREE) {
            COLORREF bg = isSelected ? theme::Color::SuccessHover : theme::Color::Success;
            theme::DrawModernButton(hdc, rc, L"🎁  DÙNG BẢN MIỄN PHÍ", bg, bg, theme::Color::TextPrimary, m_hFontBold, theme::Radius::Normal);
            return TRUE;
        }
        break;
    }

    case WM_COMMAND: {
        int id = LOWORD(wParam);
        if (id == IDC_ACT_BTN_ACTIVATE) {
            wchar_t kBuf[128]{0};
            GetWindowTextW(m_hEditKey, kBuf, 128);
            char mbKey[128]{0};
            WideCharToMultiByte(CP_UTF8, 0, kBuf, -1, mbKey, 128, nullptr, nullptr);
            std::string keyStr(mbKey);

            if (keyStr.empty()) {
                m_statusMessage = L"Vui lòng không để trống ô License Key!";
                m_statusColor = theme::Color::Danger;
                InvalidateRect(hWnd, nullptr, TRUE);
                return 0;
            }

            m_statusMessage = L"Đang kết nối xác thực License với Server...";
            m_statusColor = theme::Color::Accent;
            InvalidateRect(hWnd, nullptr, TRUE);
            UpdateWindow(hWnd);

            // Xác thực không block qua NetworkClient timeout 150ms
            std::string hwid = security::HWIDProvider::GetHWID();
            std::string resultMsg;
            bool ok = network::NetworkClient::Instance().ActivateLicense(keyStr, hwid, resultMsg);

            if (ok) {
                m_success = true;
                m_isFreeTier = false;
                m_activatedKey = keyStr;
                SaveKey(keyStr);
                m_statusMessage = L"Kích hoạt VIP thành công! Đang mở giao diện Tool...";
                m_statusColor = theme::Color::Success;
                InvalidateRect(hWnd, nullptr, TRUE);
                UpdateWindow(hWnd);

                Sleep(200);
                DestroyWindow(hWnd);
            } else {
                std::wstring wRes(resultMsg.begin(), resultMsg.end());
                m_statusMessage = wRes.empty() ? L"Key không hợp lệ hoặc máy chủ không phản hồi!" : wRes;
                m_statusColor = theme::Color::Danger;
                InvalidateRect(hWnd, nullptr, TRUE);
            }
        } else if (id == IDC_ACT_BTN_FREE) {
            m_success = true;
            m_isFreeTier = true;
            m_activatedKey = "FREE-TIER";
            m_statusMessage = L"Đã chọn Bản Miễn Phí! Đang mở giao diện Tool...";
            m_statusColor = theme::Color::Success;
            InvalidateRect(hWnd, nullptr, TRUE);
            UpdateWindow(hWnd);

            Sleep(150);
            DestroyWindow(hWnd);
        } else if (id == IDC_ACT_BTN_EXIT) {
            m_success = false;
            DestroyWindow(hWnd);
        }
        break;
    }

    case WM_CTLCOLORSTATIC: {
        HDC hdcStatic = reinterpret_cast<HDC>(wParam);
        SetTextColor(hdcStatic, theme::Color::TextPrimary);
        SetBkColor(hdcStatic, theme::Color::BgMain);
        return reinterpret_cast<INT_PTR>(m_hBgBrush);
    }

    case WM_CTLCOLOREDIT: {
        HDC hdcEdit = reinterpret_cast<HDC>(wParam);
        SetTextColor(hdcEdit, theme::Color::TextPrimary);
        SetBkColor(hdcEdit, theme::Color::Card);
        return reinterpret_cast<INT_PTR>(m_hCardBrush);
    }

    case WM_CLOSE: {
        m_success = false;
        DestroyWindow(hWnd);
        return 0;
    }

    case WM_DESTROY: {
        PostQuitMessage(0);
        return 0;
    }
    }

    return DefWindowProcW(hWnd, msg, wParam, lParam);
}

} // namespace dta::ui
