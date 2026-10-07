#pragma once
#include "client/ui/IMenuView.hpp"

namespace dta::ui::views {

class DashboardView : public IMenuView {
public:
    std::string_view GetMenuId() const override { return "tab_dashboard"; }
    std::string_view GetTitle() const override { return "Tổng Quan"; }
    const char* GetIcon() const override { return "[#]"; }

    void Render() override;
};

} // namespace dta::ui::views
