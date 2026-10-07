# 01: ĐẶC TẢ CORE C# ENGINE & IL2CPP DISPATCHER (ZERO-TAP ENGINE)

> **Mục tiêu:** Xây dựng lõi thực thi C# & Unity có tốc độ phản hồi cực hạn (độ trễ dưới 0.1ms), điều phối mọi hành động của game thông qua việc gọi trực tiếp hàm nội bộ IL2CPP trên Unity Main Thread, tương thích tự động với mọi trình giả lập (LDPlayer 9+, MEmu) và Android APK gốc.

---

## I. KIẾN TRÚC EMULATOR & BỘ NHỚ ĐA NỀN TẢNG (DEVICE ABSTRACTION)

### 1. Interface `IDeviceDriver`
Hệ thống không hardcode đường dẫn file exe hay cổng ADB. Một driver trừu tượng cho phép phát hiện tự động:

```csharp
// Core/Device/IDeviceDriver.cs
using System;

namespace DTA.Core.Device
{
    public enum EmulatorType
    {
        Unknown,
        LDPlayer9Plus,
        MEmuNewest,
        MuMuPro,
        AndroidApkNative
    }

    public interface IDeviceDriver : IDisposable
    {
        bool DetectAndAttach();
        bool IsProcessAlive { get; }
        IntPtr GetModuleBase(string moduleName);
        bool ReadMemoryRaw(IntPtr address, Span<byte> buffer);
        bool WriteMemoryRaw(IntPtr address, ReadOnlySpan<byte> buffer);
        EmulatorType Type { get; }
        string DeviceName { get; }
    }
}
```

### 2. Batch Memory Reader Siêu Tốc (`IMemoryService`)
Triệt tiêu tình trạng lag giật do gọi đọc bộ nhớ lắt nhắt hoặc qua subprocess ADB. Sử dụng Win32 Direct Memory Access hoặc Direct Memory Pointer:

```csharp
// Core/Memory/IMemoryService.cs
using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace DTA.Core.Memory
{
    public interface IMemoryService
    {
        T Read<T>(IntPtr address) where T : unmanaged;
        bool ReadBatch<T>(IntPtr baseAddress, Span<T> outSpan) where T : unmanaged;
        bool Write<T>(IntPtr address, in T value) where T : unmanaged;
        string ReadIl2CppString(IntPtr strPtr);
        bool IsValidPointer(IntPtr ptr);
    }

    public sealed class MemoryService : IMemoryService
    {
        private readonly Device.IDeviceDriver _driver;

        public MemoryService(Device.IDeviceDriver driver)
        {
            _driver = driver ?? throw new ArgumentNullException(nameof(driver));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public unsafe T Read<T>(IntPtr address) where T : unmanaged
        {
            if (!IsValidPointer(address)) return default;
            T value = default;
            Span<byte> span = new Span<byte>(&value, sizeof(T));
            _driver.ReadMemoryRaw(address, span);
            return value;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public unsafe bool ReadBatch<T>(IntPtr baseAddress, Span<T> outSpan) where T : unmanaged
        {
            if (!IsValidPointer(baseAddress)) return false;
            fixed (T* ptr = outSpan)
            {
                Span<byte> byteSpan = new Span<byte>(ptr, outSpan.Length * sizeof(T));
                return _driver.ReadMemoryRaw(baseAddress, byteSpan);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public unsafe bool Write<T>(IntPtr address, in T value) where T : unmanaged
        {
            if (!IsValidPointer(address)) return false;
            fixed (T* ptr = &value)
            {
                ReadOnlySpan<byte> byteSpan = new ReadOnlySpan<byte>(ptr, sizeof(T));
                return _driver.WriteMemoryRaw(address, byteSpan);
            }
        }

        public string ReadIl2CppString(IntPtr strPtr)
        {
            if (!IsValidPointer(strPtr)) return string.Empty;
            int length = Read<int>(strPtr + 0x10);
            if (length <= 0 || length > 2048) return string.Empty;

            Span<char> chars = stackalloc char[length];
            // Đọc UTF-16 characters trực tiếp từ offset 0x14
            Span<byte> rawBytes = MemoryMarshal.AsBytes(chars);
            if (_driver.ReadMemoryRaw(strPtr + 0x14, rawBytes))
            {
                return new string(chars);
            }
            return string.Empty;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool IsValidPointer(IntPtr ptr)
        {
            ulong addr = (ulong)ptr.ToInt64();
            return addr > 0x10000 && addr < 0x7FFFFFFFFFFF && (addr % 4 == 0);
        }
    }
}
```

---

## II. IL2CPP ACTION DISPATCHER — ĐIỀU PHỐI GỌI HÀM GAME TRÊN MAIN THREAD

### 1. Vấn Đề Cốt Lõi Của Unity
Mọi thao tác can thiệp logic game (đập đá, giật cần, vung vợt, dịch chuyển, mở UI) nếu gọi từ luồng ngoài (External Background Thread) sẽ gây crash `SIGSEGV`, vi phạm luồng hoặc hỏng Unity Garbage Collector (Boehm GC / SGen).

### 2. Giải Pháp: Unity Main Thread Dispatcher Queue
Hook vào một hàm Update của game chạy liên tục ở Main Thread (ví dụ: `KinematicCharacterMotor.UpdatePhase1` hoặc `UpdatePhase2` tại Offset `0x52F2B20` từ `libil2cpp.so`):
- Luồng bot C# đẩy lệnh (Command) vào một Concurrent Queue / Channel.
- Khi luồng chính Unity nhảy vào hàm Update đã hook, dispatcher lấy các lệnh trong queue ra và thực thi trực tiếp trong ngữ cảnh Main Thread.
- Sau khi thực thi xong, kết quả được trả về thông qua `TaskCompletionSource<T>` / `ValueTask`.

```csharp
// Core/Native/GameActionDispatcher.cs
using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;

namespace DTA.Core.Native
{
    public enum CommandPriority : byte
    {
        CancelStop = 0,
        SafetyCheck = 1,
        GameAction = 2,
        LowPriority = 3
    }

    public sealed class ActionCommand
    {
        public CommandPriority Priority { get; set; }
        public Action Action { get; set; }
        public TaskCompletionSource<bool> CompletionSource { get; set; }
    }

    public sealed class GameActionDispatcher
    {
        private static readonly ConcurrentQueue<ActionCommand> _queue = new();

        public static Task<bool> EnqueueAction(Action action, CommandPriority priority = CommandPriority.GameAction)
        {
            var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            _queue.Enqueue(new ActionCommand
            {
                Priority = priority,
                Action = action,
                CompletionSource = tcs
            });
            return tcs.Task;
        }

        /// <summary>
        /// Được gọi trực tiếp trên Unity Main Thread (tại hook Update/LateUpdate)
        /// </summary>
        public static void ProcessQueue()
        {
            int processed = 0;
            while (_queue.TryDequeue(out var cmd) && processed < 32)
            {
                try
                {
                    cmd.Action?.Invoke();
                    cmd.CompletionSource?.TrySetResult(true);
                }
                catch (Exception ex)
                {
                    cmd.CompletionSource?.TrySetException(ex);
                }
                processed++;
            }
        }
    }
}
```

---

## III. POPUP INTERCEPTOR — XỬ LÝ TRIỆT ĐỂ NÚT OK & DIALOG

Khắc phục triệt để lỗi "vẫn k gọi đc hàm action OK":
- Thay vì gọi method logic gián tiếp hoặc cố đóng form bằng cờ, bộ xử lý tìm trực tiếp địa chỉ con trỏ `UIButton` của nút OK/Confirm trên NGUI/uGUI.
- Kích hoạt sự kiện `UIButton.OnClick()` (RVA `0x524D90C`) hoặc dispatch `UICamera.Notify` trên Unity Main Thread.

```csharp
// Core/Native/PopupInterceptor.cs
using System;
using DTA.Core.Memory;

namespace DTA.Core.Native
{
    public sealed class PopupInterceptor
    {
        private readonly IMemoryService _memory;

        public PopupInterceptor(IMemoryService memory)
        {
            _memory = memory;
        }

        public bool ConfirmDialogOk(IntPtr buttonPtr)
        {
            if (!_memory.IsValidPointer(buttonPtr)) return false;

            // Đẩy lệnh gọi UIButton.OnClick() vào Main Thread
            return GameActionDispatcher.EnqueueAction(() =>
            {
                // Native Call: UIButton.OnClick(buttonPtr)
                NativeBridge.InvokeNativeVoid(NativeOffsets.UIButton_OnClick, buttonPtr);
            }, CommandPriority.SafetyCheck).GetAwaiter().GetResult();
        }
    }
}
```
