#pragma once
#include "client/ui/IMenuView.hpp"
#include "client/features/excavation/ExcavationBot.hpp"
#include <memory>

namespace dta::ui::views {

class ExcavationView : public IMenuView {
public:
    explicit ExcavationView(std::shared_ptr<features::excavation::ExcavationBot> bot);

    std::string_view GetMenuId() const override { return "tab_excavation"; }
    std::string_view GetTitle() const override { return "Đào Kho Báu"; }
    const char* GetIcon() const override { return "[v]"; }

    void Render() override;

private:
    std::shared_ptr<features::excavation::ExcavationBot> m_bot;
};

} // namespace dta::ui::views
