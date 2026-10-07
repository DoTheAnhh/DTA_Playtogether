#include "UIRenderer.hpp"
#include "DesignSystem.hpp"
#include "client/core/logger/AsyncLogger.hpp"
#include <iostream>

namespace dta::ui {

UIRenderer::UIRenderer() = default;
UIRenderer::~UIRenderer() = default;

void UIRenderer::RegisterView(std::shared_ptr<IMenuView> view) {
    if (view) {
        view->OnInit();
        m_views.push_back(std::move(view));
    }
}

void UIRenderer::SetActiveView(size_t index) {
    if (index < m_views.size()) {
        m_activeViewIndex = index;
    }
}

void UIRenderer::RenderHeader() {
    // Header UI rendering
}

void UIRenderer::RenderSidebar() {
    // Sidebar list rendering
}

void UIRenderer::RenderActiveContent() {
    if (m_activeViewIndex < m_views.size()) {
        m_views[m_activeViewIndex]->Render();
    }
}

void UIRenderer::RenderFooter() {
    // Status bar footer rendering
}

void UIRenderer::RenderFrame() {
    RenderHeader();
    RenderSidebar();
    RenderActiveContent();
    RenderFooter();
}

} // namespace dta::ui
