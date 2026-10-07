#include "ActivationDialog.hpp"
#include "client/network/NetworkClient.hpp"
#include "client/security/HWIDProvider.hpp"
#include "client/core/logger/AsyncLogger.hpp"
#include "ModernCanvas.hpp"
#include <dwmapi.h>
#include <fstream>
#include <sstream>

#pragma comment(lib, "dwmapi.lib")

namespace dta::ui {

using namespace Gdiplus;
namespace tok = dta::uikit::theme;

#define IDC_ACT_EDIT_KEY      3001
#define IDC_ACT_BTN_ACTIVATE  3002
#define IDC_ACT_BTN_EXIT      3003
#define IDC_ACT_BTN_FREE      3004

ActivationDialog& ActivationDialog::Instance() {
    static ActivationDialog instance;
    return instance;
}

ActivationDialog::ActivationDialog() {
    uikit::GdiplusScope::Instance().Init();

    m_hBgBrush = CreateSolidBrush(tok::Color::Background.Ref());
    m_hCardBrush = CreateSolidBrush(tok::Color::Panel.Ref());

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
        WS_POPUP | WS_CAPTION | WS_SYSMENU | WS_VISIBLE,
        posX, posY, winW, winH,
        nullptr, nullptr, hInstance, this
    );

    if (!m_hWnd) return false;

    // Enable Win11 Dark mode and round corners
    BOOL dark = TRUE;
    DwmSetWindowAttribute(m_hWnd, 20, &dark, sizeof(dark));
    int cornerPref = 2; // DWMWCP_ROUND
    DwmSetWindowAttribute(m_hWnd, 33, &cornerPref, sizeof(cornerPref));

    // Input Key (Flat, borderless inside modern panel)
    std::string savedKey = LoadSavedKey();
    std::wstring wKey(savedKey.begin(), savedKey.end());
    m_hEditKey = CreateWindowExW(0, L"EDIT", wKey.c_str(),
        WS_CHILD | WS_VISIBLE | ES_AUTOHSCROLL,
        40, 143, 440, 24, m_hWnd, reinterpret_cast<HMENU>(IDC_ACT_EDIT_KEY), m_hInstance, nullptr);
    SendMessage(m_hEditKey, WM_SETFONT, reinterpret_cast<WPARAM>(m_hFontMono), TRUE);
    SendMessage(m_hEditKey, EM_SETMARGINS, EC_LEFTMARGIN | EC_RIGHTMARGIN, MAKELPARAM(8, 8));

    // Nút Xác nhận VIP
    m_hBtnActivate = CreateWindowExW(0, L"BUTTON", L"✦ XÁC NHẬN VIP",
        WS_CHILD | WS_VISIBLE | BS_OWNERDRAW,
        30, 185, 330, 44, m_hWnd, reinterpret_cast<HMENU>(IDC_ACT_BTN_ACTIVATE), m_hInstance, nullptr);

    // Nút Thoát
    m_hBtnExit = CreateWindowExW(0, L"BUTTON", L"THOÁT",
        WS_CHILD | WS_VISIBLE | BS_OWNERDRAW,
        375, 185, 115, 44, m_hWnd, reinterpret_cast<HMENU>(IDC_ACT_BTN_EXIT), m_hInstance, nullptr);

    // Nút Dùng bản miễn phí
    m_hBtnFreeTier = CreateWindowExW(0, L"BUTTON", L"🎁  DÙNG BẢN MIỄN PHÍ",
        WS_CHILD | WS_VISIBLE | BS_OWNERDRAW,
        30, 280, 460, 46, m_hWnd, reinterpret_cast<HMENU>(IDC_ACT_BTN_FREE), m_hInstance, nullptr);

    ShowWindow(m_hWnd, SW_SHOW);
    UpdateWindow(m_hWnd);

    // Modal Message Loop
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
        return 1;

    case WM_PAINT: {
        PAINTSTRUCT ps;
        HDC hdcWin = BeginPaint(hWnd, &ps);

        RECT rcClient;
        GetClientRect(hWnd, &rcClient);
        int w = rcClient.right - rcClient.left;
        int h = rcClient.bottom - rcClient.top;

        HDC hdc = CreateCompatibleDC(hdcWin);
        HBITMAP hBmp = CreateCompatibleBitmap(hdcWin, w, h);
        HGDIOBJ oldBmp = SelectObject(hdc, hBmp);

        // GDI+ antialiased rendering
        {
            Graphics g(hdc);
            g.SetSmoothingMode(SmoothingModeAntiAlias);
            g.SetTextRenderingHint(TextRenderingHintClearTypeGridFit);

            // 1. Fill Background (#0B0F17)
            SolidBrush bgBrush(uikit::ToGdiPlus(tok::Color::Background));
            g.FillRectangle(&bgBrush, 0.0f, 0.0f, (float)w, (float)h);

            // 2. Header
            Font fontTitle(hdc, m_hFontTitle);
            SolidBrush cyanBrush(uikit::ToGdiPlus(tok::Color::AccentCyan));
            g.DrawString(L"✦ DTA PLAYTOGETHER", -1, &fontTitle, PointF(30.0f, 22.0f), &cyanBrush);

            Font fontReg(hdc, m_hFontRegular);
            SolidBrush textSecBrush(uikit::ToGdiPlus(tok::Color::TextSecondary));
            g.DrawString(L"Kích Hoạt Bản Quyền VIP hoặc Trải Nghiệm Bản Miễn Phí", -1, &fontReg, PointF(30.0f, 54.0f), &textSecBrush);

            // Hairline separator
            Pen linePen(uikit::ToGdiPlus(tok::Color::Hairline), 1.0f);
            g.DrawLine(&linePen, 30.0f, 84.0f, (float)(w - 30), 84.0f);

            // 3. Label
            Font fontBold(hdc, m_hFontBold);
            SolidBrush textPriBrush(uikit::ToGdiPlus(tok::Color::TextPrimary));
            g.DrawString(L"NHẬP MÃ LICENSE KEY VIP:", -1, &fontBold, PointF(30.0f, 106.0f), &textPriBrush);

            // 4. Input Container Card (Antialiased rounded box behind edit)
            RectF inputRect(30.0f, 134.0f, 460.0f, 40.0f);
            uikit::FillRoundedRect(g, inputRect, 8.0f, uikit::ToGdiPlus(tok::Color::Input));
            uikit::DrawRoundedRect(g, inputRect, 8.0f, uikit::ToGdiPlus(tok::Color::BorderStrong), 1.0f);

            // 5. Status Message Text
            SolidBrush statBrush(Color(255, GetRValue(m_statusColor), GetGValue(m_statusColor), GetBValue(m_statusColor)));
            g.DrawString(m_statusMessage.c_str(), -1, &fontBold, PointF(30.0f, 240.0f), &statBrush);

            // Divider before Free Tier
            g.DrawLine(&linePen, 30.0f, 268.0f, (float)(w - 30), 268.0f);

            // 6. Note Text
            SolidBrush mutedBrush(uikit::ToGdiPlus(tok::Color::TextMuted));
            RectF noteRect(30.0f, 342.0f, 460.0f, 60.0f);
            StringFormat noteFmt;
            noteFmt.SetAlignment(StringAlignmentNear);
            g.DrawString(L"* Bản miễn phí: Chỉ hỗ trợ Câu cá (bán toàn bộ cá, không lọc, không câu cá bóng 6-7, có khóa cam).",
                         -1, &fontReg, noteRect, &noteFmt, &mutedBrush);
        }

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

        Graphics g(hdc);
        g.SetSmoothingMode(SmoothingModeAntiAlias);
        g.SetTextRenderingHint(TextRenderingHintClearTypeGridFit);

        // 100% ZERO WHITE CORNERS: Clear button rect with window background color
        SolidBrush bgParent(uikit::ToGdiPlus(tok::Color::Background));
        g.FillRectangle(&bgParent, 0.0f, 0.0f, (float)(rc.right - rc.left), (float)(rc.bottom - rc.top));

        RectF btnRect(0.0f, 0.0f, (float)(rc.right - rc.left), (float)(rc.bottom - rc.top));
        Font font(hdc, m_hFontBold);

        if (dis->CtlID == IDC_ACT_BTN_ACTIVATE) {
            Color bg = isSelected ? uikit::ToGdiPlus(tok::Color::AccentPressed) : uikit::ToGdiPlus(tok::Color::Accent);
            uikit::DrawModernButton(g, btnRect, L"✦ XÁC NHẬN VIP", &font, bg, uikit::ToGdiPlus(tok::Color::AccentHover), uikit::ToGdiPlus(tok::Color::White), 8.0f);
            return TRUE;
        } else if (dis->CtlID == IDC_ACT_BTN_EXIT) {
            Color bg = isSelected ? uikit::ToGdiPlus(tok::Color::PanelHover) : uikit::ToGdiPlus(tok::Color::PanelAlt);
            uikit::DrawModernButton(g, btnRect, L"THOÁT", &font, bg, uikit::ToGdiPlus(tok::Color::BorderStrong), uikit::ToGdiPlus(tok::Color::TextSecondary), 8.0f);
            return TRUE;
        } else if (dis->CtlID == IDC_ACT_BTN_FREE) {
            Color bg = isSelected ? Color(255, 18, 140, 60) : uikit::ToGdiPlus(tok::Color::Success);
            uikit::DrawModernButton(g, btnRect, L"🎁  DÙNG BẢN MIỄN PHÍ", &font, bg, uikit::ToGdiPlus(tok::Color::SuccessText), uikit::ToGdiPlus(tok::Color::White), 8.0f);
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
                m_statusColor = tok::Color::Danger.Ref();
                InvalidateRect(hWnd, nullptr, FALSE);
                return 0;
            }

            m_statusMessage = L"Đang kết nối xác thực License với Server...";
            m_statusColor = tok::Color::Accent.Ref();
            InvalidateRect(hWnd, nullptr, FALSE);
            UpdateWindow(hWnd);

            std::string hwid = security::HWIDProvider::GetHWID();
            std::string resultMsg;
            bool ok = network::NetworkClient::Instance().ActivateLicense(keyStr, hwid, resultMsg);

            if (ok) {
                m_success = true;
                m_isFreeTier = false;
                m_activatedKey = keyStr;
                SaveKey(keyStr);
                MessageBoxW(hWnd, L"Kích hoạt License VIP thành công!", L"Thành Công", MB_OK | MB_ICONINFORMATION);
                DestroyWindow(hWnd);
            } else {
                m_statusMessage = L"Mã Key không hợp lệ hoặc máy chủ không phản hồi!";
                m_statusColor = tok::Color::Danger.Ref();
                InvalidateRect(hWnd, nullptr, FALSE);
            }
        } else if (id == IDC_ACT_BTN_FREE) {
            m_success = true;
            m_isFreeTier = true;
            m_activatedKey = "FREE-TIER-GUEST";
            DestroyWindow(hWnd);
        } else if (id == IDC_ACT_BTN_EXIT) {
            m_success = false;
            DestroyWindow(hWnd);
        }
        break;
    }

    case WM_CTLCOLOREDIT: {
        HDC hdcEdit = reinterpret_cast<HDC>(wParam);
        SetTextColor(hdcEdit, tok::Color::TextPrimary.Ref());
        SetBkColor(hdcEdit, tok::Color::Input.Ref());
        return reinterpret_cast<INT_PTR>(m_hCardBrush);
    }

    case WM_DESTROY:
        PostQuitMessage(0);
        return 0;
    }

    return DefWindowProcW(hWnd, msg, wParam, lParam);
}

} // namespace dta::ui
