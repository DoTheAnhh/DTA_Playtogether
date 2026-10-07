#include "NetworkClient.hpp"
#include "client/core/logger/AsyncLogger.hpp"
#include <winsock2.h>
#include <ws2tcpip.h>
#include <sstream>
#include <iostream>
#include <vector>

#pragma comment(lib, "ws2_32.lib")

namespace dta::network {

static std::string DoHttpRequest(const std::string& endpoint, const std::string& method, const std::string& path, const std::string& body) {
    std::string cleanEp = endpoint;
    if (cleanEp.starts_with("http://")) cleanEp = cleanEp.substr(7);
    if (cleanEp.starts_with("https://")) cleanEp = cleanEp.substr(8);

    std::string host = "127.0.0.1";
    int port = 28445;
    size_t colon = cleanEp.find(':');
    if (colon != std::string::npos) {
        host = cleanEp.substr(0, colon);
        size_t slash = cleanEp.find('/', colon);
        if (slash != std::string::npos) {
            port = std::stoi(cleanEp.substr(colon + 1, slash - colon - 1));
        } else {
            port = std::stoi(cleanEp.substr(colon + 1));
        }
    } else {
        size_t slash = cleanEp.find('/');
        if (slash != std::string::npos) host = cleanEp.substr(0, slash);
        else host = cleanEp;
    }

    WSADATA wsaData;
    if (WSAStartup(MAKEWORD(2, 2), &wsaData) != 0) {
        return "";
    }

    SOCKET sock = socket(AF_INET, SOCK_STREAM, IPPROTO_TCP);
    if (sock == INVALID_SOCKET) {
        WSACleanup();
        return "";
    }

    sockaddr_in saddr{};
    saddr.sin_family = AF_INET;
    saddr.sin_port = htons(static_cast<u_short>(port));
    inet_pton(AF_INET, host.c_str(), &saddr.sin_addr);

    // Sử dụng Non-blocking connect với timeout 150ms siêu tốc (ngăn chặn 100% Not Responding freeze)
    u_long nonblock = 1;
    ioctlsocket(sock, FIONBIO, &nonblock);
    connect(sock, reinterpret_cast<sockaddr*>(&saddr), sizeof(saddr));

    fd_set writeFds, errFds;
    FD_ZERO(&writeFds);
    FD_ZERO(&errFds);
    FD_SET(sock, &writeFds);
    FD_SET(sock, &errFds);

    timeval tv{0, 150000}; // 150ms timeout
    int sel = select(0, nullptr, &writeFds, &errFds, &tv);
    if (sel <= 0 || FD_ISSET(sock, &errFds)) {
        closesocket(sock);
        WSACleanup();
        return ""; // Server offline -> Trả về ngay trong 0.15s, tuyệt đối không đơ UI
    }

    // Chuyển lại blocking để gửi nhận nhanh với timeout 300ms
    u_long block = 0;
    ioctlsocket(sock, FIONBIO, &block);
    DWORD timeout = 300;
    setsockopt(sock, SOL_SOCKET, SO_RCVTIMEO, reinterpret_cast<const char*>(&timeout), sizeof(timeout));
    setsockopt(sock, SOL_SOCKET, SO_SNDTIMEO, reinterpret_cast<const char*>(&timeout), sizeof(timeout));

    std::stringstream req;
    req << method << " " << path << " HTTP/1.1\r\n"
        << "Host: " << host << ":" << port << "\r\n"
        << "Content-Type: application/json\r\n"
        << "Connection: close\r\n";
    if (!body.empty()) {
        req << "Content-Length: " << body.size() << "\r\n";
    }
    req << "\r\n";
    if (!body.empty()) {
        req << body;
    }

    std::string reqStr = req.str();
    send(sock, reqStr.data(), static_cast<int>(reqStr.size()), 0);

    std::string response;
    std::vector<char> buf(4096);
    int bytesRead = 0;
    while ((bytesRead = recv(sock, buf.data(), static_cast<int>(buf.size()), 0)) > 0) {
        response.append(buf.data(), bytesRead);
    }

    closesocket(sock);
    WSACleanup();

    size_t headerEnd = response.find("\r\n\r\n");
    if (headerEnd != std::string::npos) {
        return response.substr(headerEnd + 4);
    }
    return response;
}

NetworkClient& NetworkClient::Instance() {
    static NetworkClient instance;
    return instance;
}

NetworkClient::~NetworkClient() {
    Shutdown();
}

void NetworkClient::Init(const std::string& serverEndpoint) {
    m_serverEndpoint = serverEndpoint;
    DTA_LOG_INFO("Network", "NetworkClient initialized with endpoint: " + m_serverEndpoint);
}

void NetworkClient::Shutdown() {
    StopHeartbeat();
    m_authenticated.store(false);
}

std::string NetworkClient::HttpGet(const std::string& path) {
    return DoHttpRequest(m_serverEndpoint, "GET", path, "");
}

std::string NetworkClient::HttpPost(const std::string& path, const std::string& jsonBody) {
    return DoHttpRequest(m_serverEndpoint, "POST", path, jsonBody);
}

std::string NetworkClient::FetchUISchema() {
    std::string res = HttpGet("/api/ui");
    if (res.empty()) {
        DTA_LOG_WARN("Network", "Could not fetch UI schema from server, using built-in cached schema.");
    }
    return res;
}

std::string NetworkClient::FetchMasterTeleportSpots() {
    return HttpGet("/api/tele_positions");
}

bool NetworkClient::Authenticate(const std::string& licenseKey, const std::string& hwid) {
    std::string msg;
    return ActivateLicense(licenseKey, hwid, msg);
}

bool NetworkClient::ActivateLicense(const std::string& licenseKey, const std::string& hwid, std::string& outMsg) {
    DTA_LOG_INFO("Network", "Activating license: " + licenseKey + " for HWID: " + hwid);
    m_activeKey = licenseKey;

    std::string jsonBody = "{\"key\":\"" + licenseKey + "\", \"hwid\":\"" + hwid + "\"}";
    std::string res = HttpPost("/api/activate", jsonBody);

    if (res.empty()) {
        // If server is not responding over HTTP, allow fallback offline validation for default test key
        if (licenseKey == "DTA-VIP-2026-KEY") {
            m_authenticated.store(true);
            m_sessionToken = "sess_local_fallback";
            outMsg = "Kích hoạt Offline VIP thành công!";
            StartHeartbeat();
            return true;
        }
        outMsg = "Không thể kết nối đến Máy Chủ VPS!";
        return false;
    }

    if (res.find("\"ok\": true") != std::string::npos || res.find("\"ok\":true") != std::string::npos) {
        m_authenticated.store(true);
        m_sessionToken = "sess_vps_" + hwid.substr(0, std::min<size_t>(8, hwid.size()));
        outMsg = "Kích hoạt License VIP thành công!";
        StartHeartbeat();
        return true;
    }

    outMsg = "Key không hợp lệ hoặc đã bị khóa HWID!";
    return false;
}

void NetworkClient::StartHeartbeat() {
    if (m_heartbeatRunning.exchange(true)) return;

    m_heartbeatWorker = std::jthread([this](std::stop_token st) {
        while (!st.stop_requested() && m_heartbeatRunning.load()) {
            std::this_thread::sleep_for(std::chrono::seconds(60));
            if (st.stop_requested()) break;

            std::string reqBody = "{\"token\":\"" + m_sessionToken + "\"}";
            HttpPost("/api/check", reqBody);
            DTA_LOG_DEBUG("Network", "Sent sparse heartbeat ping to VPS.");
        }
    });
}

void NetworkClient::StopHeartbeat() {
    m_heartbeatRunning.store(false);
    if (m_heartbeatWorker.joinable()) {
        m_heartbeatWorker.request_stop();
    }
}

bool NetworkClient::SendPacket(const PacketHeader& /*header*/, std::span<const uint8_t> /*payload*/) {
    return true;
}

} // namespace dta::network
