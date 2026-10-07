#include "NativeDispatcher.hpp"
#include "client/core/logger/AsyncLogger.hpp"

namespace dta::native {

NativeDispatcher& NativeDispatcher::Instance() {
    static NativeDispatcher instance;
    return instance;
}

void NativeDispatcher::InvalidateScene(uint32_t newSceneGen) {
    std::lock_guard<std::mutex> lock(m_queueLock);
    m_currentSceneGen = newSceneGen;
    // Clear out stale tasks from old map scene
    while (!m_queue.empty()) {
        auto top = m_queue.top();
        m_queue.pop();
        try {
            top.promiseResult->set_value(nullptr);
        } catch (...) {}
    }
    DTA_LOG_INFO("Dispatcher", "Scene invalidated to gen " + std::to_string(newSceneGen));
}

size_t NativeDispatcher::GetPendingTasksCount() {
    std::lock_guard<std::mutex> lock(m_queueLock);
    return m_queue.size();
}

void NativeDispatcher::OnMainThreadTick() {
    // Process up to 10 commands per frame to avoid choking the Unity frame rate
    int processed = 0;
    while (processed < 10) {
        NativeTask task;
        {
            std::lock_guard<std::mutex> lock(m_queueLock);
            if (m_queue.empty()) break;
            task = m_queue.top();
            m_queue.pop();
        }

        // Stale command check
        if (task.sceneGen != m_currentSceneGen) {
            try {
                task.promiseResult->set_value(nullptr);
            } catch (...) {}
            continue;
        }

        void* result = nullptr;
        try {
            if (task.task) {
                result = task.task();
            }
            task.promiseResult->set_value(result);
        } catch (const std::exception& e) {
            DTA_LOG_ERROR("Dispatcher", std::string("Task failed: ") + e.what());
            try {
                task.promiseResult->set_value(nullptr);
            } catch (...) {}
        } catch (...) {
            DTA_LOG_ERROR("Dispatcher", "Task failed with unknown native error");
            try {
                task.promiseResult->set_value(nullptr);
            } catch (...) {}
        }
        processed++;
    }
}

} // namespace dta::native
