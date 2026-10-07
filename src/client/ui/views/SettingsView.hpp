#pragma once
#include "client/ui/IMenuView.hpp"
#include "client/features/settings/ISettingsService.hpp"
#include <memory>

namespace dta::ui::views {

class SettingsView : public IMenuView {
public:
    explicit SettingsView(std::shared_ptr<features::settings::ISettingsService> service);

    std::string_view GetMenuId() const override { return "tab_settings"; }
    std::string_view GetTitle() const override { return "Cài Đặt & Bảo Mật"; }
    const char* GetIcon() const override { return "[*]"; }

    void Render() override;

private:
    std::shared_ptr<features::settings::ISettingsService> m_service;
};

} // namespace dta::ui::views
