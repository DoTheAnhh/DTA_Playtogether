#pragma once
#include <functional>
#include <unordered_map>
#include <typeindex>
#include <vector>
#include <mutex>
#include <memory>

namespace dta::events {

class EventBus {
public:
    static EventBus& Instance() {
        static EventBus instance;
        return instance;
    }

    template<typename EventType, typename Callback>
    void Subscribe(Callback&& callback) {
        std::lock_guard<std::mutex> lock(m_mutex);
        auto type = std::type_index(typeid(EventType));
        m_subscribers[type].push_back([cb = std::forward<Callback>(callback)](const void* eventData) {
            cb(*static_cast<const EventType*>(eventData));
        });
    }

    template<typename EventType>
    void Publish(const EventType& event) {
        std::vector<std::function<void(const void*)>> callbacks;
        {
            std::lock_guard<std::mutex> lock(m_mutex);
            auto it = m_subscribers.find(std::type_index(typeid(EventType)));
            if (it != m_subscribers.end()) {
                callbacks = it->second;
            }
        }
        for (const auto& cb : callbacks) {
            cb(&event);
        }
    }

private:
    EventBus() = default;
    std::mutex m_mutex;
    std::unordered_map<std::type_index, std::vector<std::function<void(const void*)>>> m_subscribers;
};

// Common Events
struct SceneChangedEvent {
    uint32_t oldMapId;
    uint32_t newMapId;
};

struct DialogOpenedEvent {
    std::string dialogName;
    uintptr_t dialogPtr;
};

struct BotStateChangedEvent {
    std::string botName;
    bool isRunning;
};

} // namespace dta::events
