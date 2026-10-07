#pragma once
#include "client/ui/IMenuView.hpp"
#include "client/features/collect/CollectBot.hpp"
#include <memory>

namespace dta::ui::views {

class CollectView : public IMenuView {
public:
    explicit CollectView(std::shared_ptr<features::collect::CollectBot> bot);

    std::string_view GetMenuId() const override { return "tab_collect"; }
    std::string_view GetTitle() const override { return "Thu Thập Vật Phẩm"; }
    const char* GetIcon() const override { return "[&]"; }

    void Render() override;

private:
    std::shared_ptr<features::collect::CollectBot> m_bot;
};

} // namespace dta::ui::views
