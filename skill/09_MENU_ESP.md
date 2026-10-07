# 09: ĐẶC TẢ MENU & MODULE ESP & RADAR OVERLAY (ESP MODULE)

> **Mục tiêu:** Xây dựng hệ thống hiển thị thông tin trực quan ESP (Extra Sensory Perception) và Radar 2D thời gian thực bằng C++20 và DirectX 11 / ImGui Overlay. Đạt tốc độ khung hình 144+ FPS, tiêu thụ CPU dưới 1%, cung cấp tọa độ 3D chính xác tuyệt đối của quặng, côn trùng, cá bóng lớn và người chơi.

---

## I. KIẾN TRÚC MODULE & INTERFACES

```
src/client/features/esp/
├── IEspService.hpp         # Interface dịch vụ ESP
├── EspService.cpp          # Tính toán ma trận Camera & World-To-Screen
├── Dx11Overlay.hpp         # Cửa sổ Overlay trong suốt DirectX 11
├── Dx11Overlay.cpp         # Khởi tạo SwapChain, D3D11 Device & ImGui Context
├── Math3D.hpp              # Ma trận 4x4, Vector3, World-To-Screen Projection
├── EspModels.hpp           # Cấu trúc Cài đặt ESP & Màu sắc phân loại
└── EspView.cpp             # ImGui Render Component
```

### 1. Interface `IEspService`

```cpp
#pragma once
#include <vector>
#include "EspModels.hpp"
#include "Math3D.hpp"

class IEspService {
public:
    virtual ~IEspService() = default;

    // Chuyển đổi tọa độ 3D thế giới thành 2D màn hình
    virtual bool WorldToScreen(const Vector3& worldPos, Vector2& outScreenPos) = 0;

    // Cập nhật ma trận Camera View/Projection từ Unity Camera.main
    virtual void UpdateCameraMatrix() = 0;

    // Lấy danh sách thực thể cần vẽ trong frame hiện tại
    virtual std::vector<EspRenderEntity> GetEntitiesToRender() = 0;

    // Cấu hình hiển thị
    virtual void SetConfig(const EspConfig& config) = 0;
    virtual const EspConfig& GetConfig() const = 0;
};
```

---

## II. TOÁN HỌC WORLD-TO-SCREEN (W2S) & CAMERA MATRIX

```cpp
// include/features/esp/Math3D.hpp
struct Matrix4x4 {
    float m[4][4];
};

inline bool WorldToScreen(const Vector3& world, Vector2& screen, const Matrix4x4& viewMatrix, float screenWidth, float screenHeight) {
    float w = world.x * viewMatrix.m[0][3] + world.y * viewMatrix.m[1][3] + world.z * viewMatrix.m[2][3] + viewMatrix.m[3][3];
    if (w < 0.01f) return false; // Nằm sau lưng camera

    float x = world.x * viewMatrix.m[0][0] + world.y * viewMatrix.m[1][0] + world.z * viewMatrix.m[2][0] + viewMatrix.m[3][0];
    float y = world.x * viewMatrix.m[0][1] + world.y * viewMatrix.m[1][1] + world.z * viewMatrix.m[2][1] + viewMatrix.m[3][1];

    float invW = 1.0f / w;
    float ndcX = x * invW;
    float ndcY = y * invW;

    screen.x = (screenWidth * 0.5f) + (ndcX * screenWidth * 0.5f);
    screen.y = (screenHeight * 0.5f) - (ndcY * screenHeight * 0.5f);
    return true;
}
```

---

## III. DANH MỤC THỰC THỂ HIỂN THỊ TRÊN ESP

1. **Quặng Khoáng Sản (Ore ESP):**
   - Hộp bao quanh 3D / 2D (Bounding Box).
   - Tên quặng, Phẩm chất màu (Vàng, Xanh dương, Tím), Máu hiện tại (`HP / MaxHP`).
   - Khoảng cách (Mét) và Đường kẻ chỉ hướng (Tracer Line).
2. **Côn Trùng (Insect ESP):**
   - Vòng tròn định vị quanh bọ.
   - Nhãn tên bọ, phân loại vương miện (Crown Icon), độ cao so với mặt đất.
3. **Cá Dưới Nước (Fish ESP):**
   - Hiển thị kích cỡ bóng cá (Shadow Size 1 - 7).
   - Tên loại cá đang bơi dưới nước (nếu đọc được ID trước khi cắn).
4. **Người Chơi Xung Quanh (Player Radar & Warning):**
   - Cảnh báo khi có người chơi khác / Admin tiến lại gần trong bán kính 15m.
   - Hiển thị Nickname, khoảng cách để người dùng chủ động tạm dừng bot tránh bị soi.
5. **Rương Kho Báu & Vật Phẩm (Treasure ESP):**
   - Vị trí rương chìm dưới đất, cành cây, rác biển sự kiện.
