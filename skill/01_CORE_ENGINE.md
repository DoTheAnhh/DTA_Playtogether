# 01: ĐẶC TẢ CORE C++ ENGINE & IL2CPP DISPATCHER (ZERO-TAP ENGINE)

> **Mục tiêu:** Xây dựng lõi thực thi C++20 có tốc độ phản hồi cực hạn (độ trễ dưới 1ms), điều phối mọi hành động của game thông qua việc gọi trực tiếp hàm nội bộ IL2CPP trên Unity Main Thread, tương thích tự động với mọi trình giả lập (LDPlayer 9+, MEmu) và Android APK gốc.

---

## I. KIẾN TRÚC EMULATOR & BỘ NHỚ ĐA NỀN TẢNG (DEVICE ABSTRACTION)

### 1. Interface `IDeviceDriver`
Hệ thống không hardcode đường dẫn file exe hay cổng ADB. Một driver trừu tượng cho phép phát hiện tự động:

```cpp
// include/core/device/IDeviceDriver.hpp
#pragma once
#include <string>
#include <cstdint>
#include <span>

enum class EmulatorType {
    UNKNOWN,
    LDPLAYER_9_PLUS,
    MEMU_NEWEST,
    MUMU_PRO,
    ANDROID_APK_NATIVE
};

class IDeviceDriver {
public:
    virtual ~IDeviceDriver() = default;
    virtual bool DetectAndAttach() = 0;
    virtual bool IsProcessAlive() const = 0;
    virtual uintptr_t GetModuleBase(std::string_view moduleName) = 0;
    virtual bool ReadMemoryRaw(uintptr_t address, void* buffer, size_t size) = 0;
    virtual bool WriteMemoryRaw(uintptr_t address, const void* buffer, size_t size) = 0;
    virtual EmulatorType GetType() const = 0;
    virtual std::string GetDeviceName() const = 0;
};
```

### 2. Batch Memory Reader Siêu Tốc (`MemoryService`)
Triệt tiêu tình trạng lag giật do gọi đọc bộ nhớ lắt nhắt. Sử dụng kỹ thuật Batch Reading và C++20 Concepts:

```cpp
// include/core/memory/MemoryService.hpp
template <typename T>
concept TriviallyCopyable = std::is_trivially_copyable_v<T>;

class MemoryService {
public:
    explicit MemoryService(std::shared_ptr<IDeviceDriver> driver);

    template <TriviallyCopyable T>
    T Read(uintptr_t address) {
        T value{};
        m_driver->ReadMemoryRaw(address, &value, sizeof(T));
        return value;
    }

    template <TriviallyCopyable T>
    bool ReadBatch(uintptr_t baseAddress, std::span<T> outSpan) {
        return m_driver->ReadMemoryRaw(baseAddress, outSpan.data(), outSpan.size_bytes());
    }

    // Đọc cấu trúc chuỗi Mono/IL2CPP String an toàn
    std::string ReadIl2CppString(uintptr_t strPtr);

    // Kiểm tra con trỏ hợp lệ trước khi truy cập
    bool IsValidPointer(uintptr_t ptr) const noexcept;
};
```

---

## II. IL2CPP ACTION DISPATCHER — ĐIỀU PHỐI GỌI HÀM GAME TRÊN MAIN THREAD

### 1. Vấn Đề Cốt Lõi Của Unity
Mọi thao tác can thiệp logic game (đập đá, giật cần, vung vợt, dịch chuyển, mở UI) nếu gọi từ luồng ngoài (External Thread) sẽ gây crash `SIGSEGV` hoặc hỏng Unity Garbage Collector (Boehm GC / SGen).

### 2. Giải Pháp: Unity Main Thread Dispatcher Queue
Hook vào một hàm Update của game chạy liên tục ở Main Thread (ví dụ: `KinematicCharacterMotor.UpdatePhase1` hoặc `UpdatePhase2` tại Offset `0x52F2B20` từ `libil2cpp.so`):
- Luồng bot C++ đẩy lệnh (Command) vào một Lock-free Concurrent Queue.
- Khi luồng chính Unity nhảy vào hàm Update đã hook, dispatcher lấy các lệnh trong queue ra và thực thi trực tiếp trong ngữ cảnh Main Thread.
- Sau khi thực thi xong, kết quả được trả về thông qua `std::promise` / `std::future`.

```cpp
// include/core/native/NativeDispatcher.hpp
#pragma once
#include <functional>
#include <future>
#include <queue>
#include <mutex>
#include "shared/Types.hpp"

enum class CommandPriority : uint8_t {
    CANCEL_STOP = 0,
    SAFETY_CHECK = 1,
    GAME_ACTION = 2,
    LOW_PRIORITY = 3
};

struct NativeCommand {
    CommandPriority priority;
    std::function<void*()> task;
    std::promise<void*> promiseResult;
    uint32_t sceneGen; // Chống thực thi lệnh stale khi đã đổi map
};

class NativeDispatcher {
public:
    static NativeDispatcher& Instance();

    // Đẩy tác vụ vào thực thi trên Unity Main Thread và chờ kết quả có timeout
    template<typename Func>
    auto EnqueueMainThread(CommandPriority priority, Func&& func, uint32_t timeoutMs = 1500) {
        // Thực thi an toàn với std::future
    }

    // Hàm hook được gọi trực tiếp mỗi frame từ Unity Main Thread
    void OnMainThreadTick();

private:
    std::priority_queue<NativeCommand> m_queue;
    std::mutex m_queueLock;
};
```

---

## III. BẢNG TRA CỨU HÀM NATIVE CỐT LÕI (VERIFIED TỪ DUMP.CS)

| Tên Lớp & Phương Thức | RVA Offset (Base `libil2cpp.so`) | Tham Số (Calling Convention) | Mô Tả & Tác Dụng |
| :--- | :--- | :--- | :--- |
| `KinematicCharacterMotor.set_TransientPosition` | `0x52F2B20` | `(void* this, Vector3 value)` | Dịch chuyển nhân vật tức thời không tốn frame |
| `LayerSystem.ConnectToZoneMove` | `0x5A13410` | `(void* this, uint32 mapId, int32 from, void* server, bool iris, bool adapt)` | Chuyển map trực tiếp không qua NPC/Portal |
| `FishingPoleController.OnClick_Button` | `0x4EBD72C` | `(void* this, int32 btnIndex)` | Thả cần / giật cần câu trực tiếp |
| `PickaxController.OnClick_Button` | `0x597829C` | `(void* this, int32 btnIndex)` | Vung cuốc đập đá tức thì |
| `ShovelController.OnClick_Button` | `0x5978AF4` | `(void* this, int32 btnIndex)` | Cắm xẻng đào kho báu |
| `InsectNetController.OnClick_Button` | `0x4F77F60` | `(void* this, int32 btnIndex)` | Vung vợt bắt côn trùng |
| `AttachActionButton.OnClickActionButton` | `0x4CEB6A8` | `(void* this, int32 index)` | Kích hoạt công cụ đang cầm chung |
| `DialogJoyStick.OnPress_JumpButton` | `0x5E70584` | `(void* this)` | Nhấn nút Jump / Action |
| `DialogJoyStick.OnRelease_JumpButton` | `0x5E708D8` | `(void* this)` | Thả nút Jump / Action |
| `UIButton.OnClick` | `0x524D90C` | `(void* this)` | Kích hoạt bất kỳ UI button nào của game |
| `DialogBoxMessage.OnClick_OK` | `0x5A29F80` | `(void* this)` | Tự xác nhận đóng thông báo hệ thống |
| `DialogBoxQuestion.OnClick_OK` | `0x5A2A0F0` | `(void* this)` | Bấm xác nhận câu hỏi xác nhận (Yes) |
| `DialogBoxQuestion.OnClick_Cancel`| `0x5A2A170` | `(void* this)` | Bấm hủy bỏ câu hỏi xác nhận (No) |
| `DialogRewardPopup.OnClickYes` | `0x5A31C00` | `(void* this)` | Nhận quà / nhận thưởng sự kiện |
| `DialogResultGetItemView.OnClick_ButtonSkip` | `0x4D370D8` | `(void* this)` | Bỏ qua hoạt ảnh nhận vật phẩm |
| `DialogResultGetItemView.OnClick_ButtonClose`| `0x4D372CC` | `(void* this)` | Đóng bảng nhận vật phẩm tức thì |
| `DialogFishingGetItem.OnClick_ButtonClose` | `0x5DE6BB4` | `(void* this)` | Bảo quản cá câu được |
| `DialogFishingGetItem.OnClick_Selling` | `0x5DE9C7C` | `(void* this)` | Bán nhanh cá câu được |
| `DialogItemRepair.OnClick_Repair` | `0x5E6AC7C` | `(void* this)` | Sửa cần / sửa cuốc hỏng tức thì |
| `DialogItemRepair.OnClick_Close` | `0x5E6AD68` | `(void* this)` | Đóng bảng sửa đồ |
| `OnPickFieldObject` | `0x57CE884` | `(void* actor, uint32 uid)` | Nhặt vật thể rơi trên mặt đất |

---

## IV. POPUP INTERCEPTOR SERVICE (TỰ ĐỘNG XỬ LÝ HỘP THOẠI)

Trong quá trình bot chạy tự động, các popup bất ngờ (hết độ bền, phần thưởng nhiệm vụ, thông báo mạng, kết nối lại) có thể chặn luồng game. `PopupInterceptorService` chạy ngầm để phát hiện và gọi hàm đóng ngay lập tức:
- Kiểm tra danh sách Active Dialogs trong `UIManager` của game.
- Nếu phát hiện dialog thuộc danh sách trắng (White-listed), tự động gọi hàm xác nhận hoặc đóng tương ứng.
- Tuyệt đối không để popup làm treo State Machine của bot.
