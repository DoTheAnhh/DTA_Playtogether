#pragma once
#include "client/ui/IMenuView.hpp"
#include "client/features/fishing/FishingBot.hpp"
#include <memory>

namespace dta::ui::views {

class FishingView : public IMenuView {
public:
    explicit FishingView(std::shared_ptr<features::fishing::FishingBot> bot);

    std::string_view GetMenuId() const override { return "tab_fishing"; }
    std::string_view GetTitle() const override { return "Câu Cá Siêu Tốc"; }
    const char* GetIcon() const override { return "[~]"; }

    void Render() override;

private:
    std::shared_ptr<features::fishing::FishingBot> m_bot;
};

} // namespace dta::ui::views
