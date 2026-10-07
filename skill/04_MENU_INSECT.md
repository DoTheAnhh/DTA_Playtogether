# 04: ĐẶC TẢ MENU & MODULE BẮT CÔN TRÙNG (INSECT MODULE)

> **Mục tiêu:** Tái thiết kế toàn bộ hệ thống bắt côn trùng tự động bằng C# & Unity. Tối ưu thuật toán quét bọ, dự đoán vector vận tốc và hướng bay (Velocity Trajectory Prediction), tiếp cận góc đón đầu chính xác tuyệt đối, vung vợt bắt bọ thông qua gọi hàm game và tự đóng bảng kết quả ngay lập tức.

---

## I. KIẾN TRÚC MODULE & INTERFACES

```
src/Client/Features/Insect/
├── IInsectService.cs       # Interface dịch vụ bắt bọ
├── InsectService.cs        # Tương tác IL2CPP & quản lý net controller
├── InsectBot.cs            # State Machine điều khiển chu trình bắt bọ
├── InsectPredictor.cs      # Dự đoán tọa độ đón đầu côn trùng đang bay
├── InsectModels.cs         # Cấu trúc Côn trùng, Bộ lọc phẩm cấp & Thống kê
└── InsectView.cs           # Unity UI View Component
```

### 1. Interface `IInsectService`

```csharp
using System;
using System.Collections.Generic;
using System.Numerics;
using System.Threading.Tasks;

namespace DTA.Features.Insect
{
    public interface IInsectService
    {
        // Các hành động gọi hàm game trực tiếp
        Task<bool> SwingNetAsync();                     // Vung vợt bắt bọ
        Task<bool> ApproachInsectAsync(Vector3 targetPos, float yawAngle); // Đón đầu
        Task<bool> FreezeInsectAsync(uint insectUid);   // Đóng băng tốc độ bay
        Task<bool> RepairNetAsync();                    // Sửa vợt khi hỏng
        Task<bool> CloseResultDialogAsync();            // Đóng bảng nhận bọ

        // Dò tìm và lọc côn trùng từ bộ nhớ
        IReadOnlyList<InsectEntity> ScanInsectsAround(float radius);
        InsectEntity GetBestTargetInsect();
        bool IsNetBroken();
    }
}
```

---

## II. THUẬT TOÁN ĐÓN ĐẦU & VUNG VỢT CHÍNH XÁC (INTERCEPT ALGORITHM)

Khác với quặng đá đứng yên, côn trùng liên tục di chuyển:
1. **Velocity Tracking:** Đọc liên tục vị trí $P(t)$ và $P(t - \Delta t)$ để tính vector vận tốc $\vec{v}$.
2. **Intercept Point Calculation:** Tính điểm đón đầu $P_{\text{target}} = P_{\text{current}} + \vec{v} \cdot t_{\text{swing}}$ (với $t_{\text{swing}} \approx 120\text{ms}$).
3. **Approach & Orientation:** Dịch chuyển người chơi tới vị trí cách $P_{\text{target}}$ khoảng $1.8\text{m}$ theo hướng đón đầu, đồng thời set góc quay nhân vật hướng thẳng vào bọ.
4. **Trigger Action:** Gọi ngay `InsectNetController.OnClick_Button(0)` trên Main Thread.

---

## III. BẢNG MÃ HÀM IL2CPP BẮT CÔN TRÙNG (DUMP.CS)

| Chức Năng | Tên Hàm Game Trong `dump.cs` | Offset RVA | Tham Số & Ghi Chú |
| :--- | :--- | :--- | :--- |
| **Vung Vợt** | `InsectNetController.OnClick_Button` | `0x57D3800` | `(thisPtr, int actionType=0)` trên Main Thread |
| **Trạng Thái Vợt** | Field `m_eState` trong `InsectNetController` | Offset `0x98` | Trạng thái vung vợt |
| **Độ Bền Vợt** | Field `m_CurrentDurability` | Offset `0xA0` | Độ bền vợt |
| **Đóng Dialog Bọ** | `InsectResultDialog.OnClick_OK` | `0x57E5400` | Đóng bảng popup bắt được bọ |
