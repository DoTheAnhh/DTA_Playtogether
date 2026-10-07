#pragma once
#include <windows.h>
#include <string>

namespace dta::ui {

class ActivationDialog {
public:
    static ActivationDialog& Instance();

    // Hiển thị modal dialog nhập key. Trả về true nếu kích hoạt thành công hoặc chọn bản miễn phí, false nếu thoát/hủy.
    bool ShowModal(HINSTANCE hInstance, std::string& outLicenseKey, bool& outIsFreeTier);

private:
    ActivationDialog();
    ~ActivationDialog();

    static LRESULT CALLBACK WndProc(HWND hWnd, UINT msg, WPARAM wParam, LPARAM lParam);
    LRESULT HandleMessage(HWND hWnd, UINT msg, WPARAM wParam, LPARAM lParam);

    std::string LoadSavedKey();
    void SaveKey(const std::string& key);

    HINSTANCE m_hInstance{nullptr};
    HWND m_hWnd{nullptr};
    HWND m_hEditKey{nullptr};
    HWND m_hBtnActivate{nullptr};
    HWND m_hBtnFreeTier{nullptr};
    HWND m_hBtnExit{nullptr};

    HBRUSH m_hBgBrush{nullptr};
    HBRUSH m_hCardBrush{nullptr};
    HFONT m_hFontTitle{nullptr};
    HFONT m_hFontRegular{nullptr};
    HFONT m_hFontBold{nullptr};
    HFONT m_hFontMono{nullptr};

    std::string m_activatedKey;
    bool m_success{false};
    bool m_isFreeTier{false};
    std::wstring m_statusMessage{L"Vui lòng nhập License Key VIP hoặc chọn Dùng bản miễn phí."};
    COLORREF m_statusColor{RGB(148, 163, 184)};
};

} // namespace dta::ui
