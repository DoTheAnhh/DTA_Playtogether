#pragma once
#include "client/ui/IMenuView.hpp"
#include "client/features/esp/IEspService.hpp"
#include <memory>

namespace dta::ui::views {

class EspView : public IMenuView {
public:
    explicit EspView(std::shared_ptr<features::esp::IEspService> service);

    std::string_view GetMenuId() const override { return "tab_esp"; }
    std::string_view GetTitle() const override { return "ESP & Radar"; }
    const char* GetIcon() const override { return "[@]"; }

    void Render() override;

private:
    std::shared_ptr<features::esp::IEspService> m_service;
};

} // namespace dta::ui::views
