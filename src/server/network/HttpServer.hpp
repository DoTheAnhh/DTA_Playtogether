#pragma once
#include <string>
#include <atomic>
#include <thread>
#include <functional>
#include <winsock2.h>
#include <ws2tcpip.h>

namespace dta::server::network {

class HttpServer {
public:
    explicit HttpServer(uint16_t port = 28445);
    ~HttpServer();

    bool Start();
    void Stop();
    [[nodiscard]] bool IsRunning() const noexcept { return m_running.load(); }
    [[nodiscard]] uint16_t GetPort() const noexcept { return m_port; }

private:
    uint16_t m_port;
    SOCKET m_listenSocket{INVALID_SOCKET};
    std::atomic<bool> m_running{false};
    std::jthread m_listenerThread;

    void ListenerLoop(std::stop_token st);
    void HandleClient(SOCKET clientSocket, const std::string& clientIp);
};

} // namespace dta::server::network
