#pragma once
#include <string_view>

namespace dta::ui {

class IMenuView {
public:
    virtual ~IMenuView() = default;

    [[nodiscard]] virtual std::string_view GetMenuId() const = 0;
    [[nodiscard]] virtual std::string_view GetTitle() const = 0;
    [[nodiscard]] virtual const char* GetIcon() const = 0;

    virtual void OnInit() {}
    virtual void Render() = 0;
    virtual void OnDestroy() {}
};

} // namespace dta::ui
