#pragma once
#include "client/ui/IMenuView.hpp"
#include "client/features/mining/MiningBot.hpp"
#include <memory>

namespace dta::ui::views {

class MiningView : public IMenuView {
public:
    explicit MiningView(std::shared_ptr<features::mining::MiningBot> bot);

    std::string_view GetMenuId() const override { return "tab_mining"; }
    std::string_view GetTitle() const override { return "Đập Đá & Quặng"; }
    const char* GetIcon() const override { return "[*]"; }

    void Render() override;

private:
    std::shared_ptr<features::mining::MiningBot> m_bot;
};

} // namespace dta::ui::views
