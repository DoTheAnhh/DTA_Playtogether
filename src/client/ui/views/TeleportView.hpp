#pragma once
#include "client/ui/IMenuView.hpp"
#include "client/features/teleport/ITeleportService.hpp"
#include <memory>

namespace dta::ui::views {

class TeleportView : public IMenuView {
public:
    explicit TeleportView(std::shared_ptr<features::teleport::ITeleportService> service);

    std::string_view GetMenuId() const override { return "tab_teleport"; }
    std::string_view GetTitle() const override { return "Dịch Chuyển & Map"; }
    const char* GetIcon() const override { return "[^]"; }

    void Render() override;

private:
    std::shared_ptr<features::teleport::ITeleportService> m_service;
};

} // namespace dta::ui::views
