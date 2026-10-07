#pragma once
#include "client/ui/IMenuView.hpp"
#include "client/features/insect/InsectBot.hpp"
#include <memory>

namespace dta::ui::views {

class InsectView : public IMenuView {
public:
    explicit InsectView(std::shared_ptr<features::insect::InsectBot> bot);

    std::string_view GetMenuId() const override { return "tab_insect"; }
    std::string_view GetTitle() const override { return "Bắt Côn Trùng"; }
    const char* GetIcon() const override { return "[o]"; }

    void Render() override;

private:
    std::shared_ptr<features::insect::InsectBot> m_bot;
};

} // namespace dta::ui::views
