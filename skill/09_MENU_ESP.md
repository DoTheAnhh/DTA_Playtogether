# 09: ĐẶC TẢ MENU & MODULE ESP & RADAR OVERLAY (ESP MODULE)

> **Mục tiêu:** Xây dựng hệ thống hiển thị thông tin trực quan ESP (Extra Sensory Perception) và Radar 2D thời gian thực bằng C# & Unity (Unity Canvas / uGUI / IMGUI Overlay). Đạt tốc độ khung hình 144+ FPS, tiêu thụ CPU dưới 1%, cung cấp tọa độ 3D chính xác tuyệt đối của quặng, côn trùng, cá bóng lớn và người chơi.

---

## I. KIẾN TRÚC MODULE & INTERFACES

```
src/Client/Features/Esp/
├── IEspService.cs          # Interface dịch vụ ESP
├── EspService.cs           # Tính toán ma trận Camera & World-To-Screen
├── EspOverlay.cs           # Unity Canvas / IMGUI Overlay Renderer
├── Math3D.cs               # Ma trận 4x4, Vector3, World-To-Screen Projection
├── EspModels.cs            # Cấu trúc Cài đặt ESP & Màu sắc phân loại
└── EspView.cs              # Unity UI View Component
```

### 1. Interface `IEspService`

```csharp
using System.Collections.Generic;
using System.Numerics;

namespace DTA.Features.Esp
{
    public interface IEspService
    {
        // Chuyển đổi tọa độ 3D thế giới thành 2D màn hình
        bool WorldToScreen(Vector3 worldPos, out Vector2 screenPos);

        // Cập nhật ma trận Camera View/Projection từ Unity Camera.main
        void UpdateCameraMatrix();

        // Lấy danh sách thực thể cần vẽ trong frame hiện tại
        IReadOnlyList<EspRenderEntity> GetEntitiesToRender();

        // Cấu hình hiển thị
        void SetConfig(in EspConfig config);
        ref readonly EspConfig GetConfig();
    }
}
```

---

## II. TOÁN HỌC WORLD-TO-SCREEN (W2S) & CAMERA MATRIX

```csharp
using System.Numerics;
using System.Runtime.CompilerServices;

namespace DTA.Features.Esp
{
    public static class Math3D
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool WorldToScreen(
            in Vector3 world, 
            out Vector2 screen, 
            in Matrix4x4 viewProjMatrix, 
            float screenWidth, 
            float screenHeight)
        {
            float w = world.X * viewProjMatrix.M14 + world.Y * viewProjMatrix.M24 + world.Z * viewProjMatrix.M34 + viewProjMatrix.M44;
            if (w < 0.01f)
            {
                screen = Vector2.Zero;
                return false;
            }

            float invW = 1.0f / w;
            float x = (world.X * viewProjMatrix.M11 + world.Y * viewProjMatrix.M21 + world.Z * viewProjMatrix.M31 + viewProjMatrix.M41) * invW;
            float y = (world.X * viewProjMatrix.M12 + world.Y * viewProjMatrix.M22 + world.Z * viewProjMatrix.M32 + viewProjMatrix.M42) * invW;

            screen = new Vector2(
                (screenWidth / 2f) * (1f + x),
                (screenHeight / 2f) * (1f - y)
            );
            return true;
        }
    }
}
```
