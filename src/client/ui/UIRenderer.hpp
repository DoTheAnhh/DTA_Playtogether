#pragma once
#include <vector>
#include <memory>
#include <string>
#include "IMenuView.hpp"

namespace dta::ui {

class UIRenderer {
public:
    UIRenderer();
    ~UIRenderer();

    void RegisterView(std::shared_ptr<IMenuView> view);
    void SetActiveView(size_t index);

    void RenderHeader();
    void RenderSidebar();
    void RenderActiveContent();
    void RenderFooter();

    void RenderFrame();

    [[nodiscard]] size_t GetActiveViewIndex() const noexcept { return m_activeViewIndex; }

private:
    std::vector<std::shared_ptr<IMenuView>> m_views;
    size_t m_activeViewIndex{0};
};

} // namespace dta::ui
