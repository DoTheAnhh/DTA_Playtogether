#pragma once
#include <functional>
#include <future>
#include <queue>
#include <mutex>
#include <chrono>
#include "shared/GameEnums.hpp"

namespace dta::native {

struct NativeTask {
    CommandPriority priority{CommandPriority::GAME_ACTION};
    std::function<void*()> task;
    std::shared_ptr<std::promise<void*>> promiseResult;
    uint32_t sceneGen{0};
    uint64_t enqueueTimeMs{0};

    bool operator<(const NativeTask& other) const noexcept {
        // Lower enum value means higher priority
        return static_cast<uint8_t>(priority) > static_cast<uint8_t>(other.priority);
    }
};

class NativeDispatcher {
public:
    static NativeDispatcher& Instance();

    template<typename Func>
    auto EnqueueMainThread(CommandPriority priority, Func&& func, uint32_t timeoutMs = 1500) {
        using ReturnType = decltype(func());
        auto promise = std::make_shared<std::promise<void*>>();
        std::future<void*> fut = promise->get_future();

        NativeTask task;
        task.priority = priority;
        task.promiseResult = promise;
        task.sceneGen = m_currentSceneGen;
        task.task = [f = std::forward<Func>(func)]() -> void* {
            if constexpr (std::is_void_v<ReturnType>) {
                f();
                return nullptr;
            } else {
                return reinterpret_cast<void*>(f());
            }
        };

        {
            std::lock_guard<std::mutex> lock(m_queueLock);
            m_queue.push(task);
        }

        // Wait for execution on Main Thread
        if (fut.wait_for(std::chrono::milliseconds(timeoutMs)) == std::future_status::ready) {
            void* rawRes = fut.get();
            if constexpr (!std::is_void_v<ReturnType>) {
                return reinterpret_cast<ReturnType>(rawRes);
            }
        }
    }

    void OnMainThreadTick();
    void InvalidateScene(uint32_t newSceneGen);
    [[nodiscard]] size_t GetPendingTasksCount();

private:
    NativeDispatcher() = default;
    std::priority_queue<NativeTask> m_queue;
    std::mutex m_queueLock;
    uint32_t m_currentSceneGen{0};
};

} // namespace dta::native
