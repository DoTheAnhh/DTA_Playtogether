#include "HttpServer.hpp"
#include "server/api/AuthHandler.hpp"
#include "server/ui_schema/UISchemaManager.hpp"
#include "server/security/TokenBucketLimiter.hpp"
#include "server/database/DatabaseManager.hpp"
#include "server/services/MasterTeleportService.hpp"
#include "shared/PacketProtocol.hpp"
#include <iostream>
#include <sstream>
#include <vector>

#pragma comment(lib, "ws2_32.lib")

namespace dta::server::network {

HttpServer::HttpServer(uint16_t port)
    : m_port(port) {}

HttpServer::~HttpServer() {
    Stop();
}

bool HttpServer::Start() {
    if (m_running.exchange(true)) return true;

    WSADATA wsaData;
    int wsaRes = WSAStartup(MAKEWORD(2, 2), &wsaData);
    if (wsaRes != 0) {
        std::cerr << "[-] WSAStartup failed: " << wsaRes << "\n";
        m_running.store(false);
        return false;
    }

    m_listenSocket = socket(AF_INET, SOCK_STREAM, IPPROTO_TCP);
    if (m_listenSocket == INVALID_SOCKET) {
        std::cerr << "[-] Failed to create socket: " << WSAGetLastError() << "\n";
        WSACleanup();
        m_running.store(false);
        return false;
    }

    int opt = 1;
    setsockopt(m_listenSocket, SOL_SOCKET, SO_REUSEADDR, reinterpret_cast<const char*>(&opt), sizeof(opt));

    sockaddr_in serverAddr{};
    serverAddr.sin_family = AF_INET;
    serverAddr.sin_addr.s_addr = INADDR_ANY;
    serverAddr.sin_port = htons(m_port);

    if (bind(m_listenSocket, reinterpret_cast<sockaddr*>(&serverAddr), sizeof(serverAddr)) == SOCKET_ERROR) {
        std::cerr << "[-] Socket bind failed on port " << m_port << ": " << WSAGetLastError() << "\n";
        closesocket(m_listenSocket);
        m_listenSocket = INVALID_SOCKET;
        WSACleanup();
        m_running.store(false);
        return false;
    }

    if (listen(m_listenSocket, SOMAXCONN) == SOCKET_ERROR) {
        std::cerr << "[-] Socket listen failed: " << WSAGetLastError() << "\n";
        closesocket(m_listenSocket);
        m_listenSocket = INVALID_SOCKET;
        WSACleanup();
        m_running.store(false);
        return false;
    }

    std::cout << "[+] Server listening on http://0.0.0.0:" << m_port << "\n";

    m_listenerThread = std::jthread([this](std::stop_token st) {
        ListenerLoop(st);
    });

    return true;
}

void HttpServer::Stop() {
    if (!m_running.exchange(false)) return;

    if (m_listenSocket != INVALID_SOCKET) {
        closesocket(m_listenSocket);
        m_listenSocket = INVALID_SOCKET;
    }

    if (m_listenerThread.joinable()) {
        m_listenerThread.request_stop();
    }

    WSACleanup();
    std::cout << "[*] HTTP Server stopped.\n";
}

void HttpServer::ListenerLoop(std::stop_token st) {
    while (!st.stop_requested() && m_running.load()) {
        sockaddr_in clientAddr{};
        int clientLen = sizeof(clientAddr);
        SOCKET clientSocket = accept(m_listenSocket, reinterpret_cast<sockaddr*>(&clientAddr), &clientLen);

        if (clientSocket == INVALID_SOCKET) {
            if (!m_running.load()) break;
            continue;
        }

        char ipStr[INET_ADDRSTRLEN]{0};
        inet_ntop(AF_INET, &(clientAddr.sin_addr), ipStr, INET_ADDRSTRLEN);
        std::string clientIp(ipStr);

        // Detach client handling thread
        std::thread([this, clientSocket, clientIp]() {
            HandleClient(clientSocket, clientIp);
        }).detach();
    }
}

void HttpServer::HandleClient(SOCKET clientSocket, const std::string& clientIp) {
    std::vector<char> buffer(4096, 0);
    int bytesRecv = recv(clientSocket, buffer.data(), static_cast<int>(buffer.size()) - 1, 0);

    if (bytesRecv <= 0) {
        closesocket(clientSocket);
        return;
    }

    std::string requestStr(buffer.data(), bytesRecv);
    std::istringstream reqStream(requestStr);
    std::string method, path, version;
    reqStream >> method >> path >> version;

    // Extract body if any
    std::string body;
    size_t headerEnd = requestStr.find("\r\n\r\n");
    if (headerEnd != std::string::npos) {
        body = requestStr.substr(headerEnd + 4);
    }

    std::string responseBody;
    std::string statusCode = "200 OK";

    if (path == "/api/activate" || path == "/api/check") {
        dta::AuthRequest req;
        // Parse simple JSON fields
        auto parseField = [](const std::string& json, const std::string& key) -> std::string {
            size_t kpos = json.find("\"" + key + "\"");
            if (kpos == std::string::npos) return "";
            size_t colon = json.find(":", kpos);
            if (colon == std::string::npos) return "";
            size_t valStart = json.find("\"", colon);
            if (valStart == std::string::npos) return "";
            size_t valEnd = json.find("\"", valStart + 1);
            if (valEnd == std::string::npos) return "";
            return json.substr(valStart + 1, valEnd - valStart - 1);
        };

        req.licenseKey = parseField(body, "key");
        if (req.licenseKey.empty()) req.licenseKey = parseField(body, "licenseKey");
        req.hwid = parseField(body, "device");
        if (req.hwid.empty()) req.hwid = parseField(body, "hwid");
        if (req.licenseKey.empty()) req.licenseKey = "DTA-VIP-2026-KEY";

        auto resp = api::AuthHandler::Instance().HandleAuth(clientIp, req);
        responseBody = "{\n  \"ok\": " + std::string(resp.success ? "true" : "false") + ",\n  \"msg\": \"" +
                       resp.message + "\",\n  \"token\": \"" + resp.sessionToken + "\"\n}";
    } else if (path == "/api/ui") {
        responseBody = ui_schema::UISchemaManager::Instance().GetSchemaJson();
    } else if (path == "/api/tele_positions") {
        if (method == "POST") {
            // Parse spot from body
            auto parseStr = [](const std::string& json, const std::string& key) -> std::string {
                size_t p = json.find("\"" + key + "\"");
                if (p == std::string::npos) return "";
                size_t c = json.find(":", p);
                if (c == std::string::npos) return "";
                size_t s = json.find("\"", c);
                if (s == std::string::npos) return "";
                size_t e = json.find("\"", s + 1);
                if (e == std::string::npos) return "";
                return json.substr(s + 1, e - s - 1);
            };
            auto parseNum = [](const std::string& json, const std::string& key) -> float {
                size_t p = json.find("\"" + key + "\"");
                if (p == std::string::npos) return 0.0f;
                size_t c = json.find(":", p);
                if (c == std::string::npos) return 0.0f;
                size_t start = json.find_first_of("-0123456789.", c);
                if (start == std::string::npos) return 0.0f;
                size_t end = json.find_first_not_of("-0123456789.", start);
                return std::stof(json.substr(start, end - start));
            };
            services::MasterTeleportSpot spot;
            spot.id = parseStr(body, "id");
            if (spot.id.empty()) spot.id = "spot_" + std::to_string(GetTickCount64());
            spot.name = parseStr(body, "name");
            spot.mapId = static_cast<uint32_t>(parseNum(body, "mapId"));
            spot.mapName = parseStr(body, "mapName");
            spot.position.x = parseNum(body, "x");
            spot.position.y = parseNum(body, "y");
            spot.position.z = parseNum(body, "z");
            spot.enabled = true;
            services::MasterTeleportService::Instance().AddOrUpdateSpot(spot);
            responseBody = "{\"ok\":true,\"msg\":\"Spot added successfully\"}";
        } else {
            responseBody = services::MasterTeleportService::Instance().GetSpotsJson();
        }
    } else if (path == "/api/keys") {
        auto keys = database::DatabaseManager::Instance().GetAllKeys();
        std::stringstream ss;
        ss << "[\n";
        for (size_t i = 0; i < keys.size(); ++i) {
            ss << "  {\"key\": \"" << keys[i].licenseKey << "\", \"hwid\": \"" << keys[i].boundHWID 
               << "\", \"active\": " << (keys[i].isActive ? "true" : "false") << "}"
               << (i + 1 < keys.size() ? ",\n" : "\n");
        }
        ss << "]";
        responseBody = ss.str();
    } else if (path == "/health" || path == "/") {
        responseBody = "{\"status\":\"online\",\"server\":\"DTA High-Performance VPS Server v3.0\",\"port\":" + std::to_string(m_port) + "}";
    } else {
        statusCode = "404 Not Found";
        responseBody = "{\"ok\":false,\"msg\":\"Endpoint not found\"}";
    }

    std::stringstream httpResp;
    httpResp << "HTTP/1.1 " << statusCode << "\r\n"
             << "Content-Type: application/json; charset=utf-8\r\n"
             << "Content-Length: " << responseBody.size() << "\r\n"
             << "Access-Control-Allow-Origin: *\r\n"
             << "Connection: close\r\n\r\n"
             << responseBody;

    std::string rawResp = httpResp.str();
    send(clientSocket, rawResp.data(), static_cast<int>(rawResp.size()), 0);
    closesocket(clientSocket);
}

} // namespace dta::server::network
