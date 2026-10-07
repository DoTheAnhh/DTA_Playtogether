#pragma once
#include "client/ui/IMenuView.hpp"
#include "client/features/farm/FarmBot.hpp"
#include <memory>

namespace dta::ui::views {

class FarmView : public IMenuView {
public:
    explicit FarmView(std::shared_ptr<features::farm::FarmBot> bot);

    std::string_view GetMenuId() const override { return "tab_farm"; }
    std::string_view GetTitle() const override { return "Nông Trại"; }
    const char* GetIcon() const override { return "[+]"; }

    void Render() override;

private:
    std::shared_ptr<features::farm::FarmBot> m_bot;
};

} // namespace dta::ui::views
