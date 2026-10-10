using DTA.Runtime.Core;

namespace DTA.Game.Session;

/// <summary>Phần nhân vật + hệ thống con + số mã hoá của <see cref="GameSession"/>.</summary>
public sealed partial class GameSession
{
    /// <summary>ActorSystem.Self -> myActorCharacter; EncryptSystem._instance -> EncryptInt._storageDic.</summary>
    private void BindActorSystem()
    {
        var actorSystem = _classes["ActorSystem"];
        var encrypt = _classes["PT_Encrypt.EncryptSystem"];
        _actorSystemSlot = Il2Cpp.StaticFieldPtr(actorSystem, "Self");
        _myActorOffset = Il2Cpp.Field(actorSystem, "myActorCharacter");
        var instance = (long)Memory.U64(Il2Cpp.StaticFieldPtr(encrypt, "_instance"));
        var storage = instance != 0 ? (long)Memory.U64(instance + Il2Cpp.Field(encrypt, "EncryptInt")) : 0;
        if (_actorSystemSlot == 0 || storage == 0) throw new GameError("Game chưa vào tới màn chơi", true);
        _storageDicSlot = storage + Il2Cpp.Field(Managed.ClassOf(storage), "_storageDic");
    }

    /// <summary>Ô giữ con trỏ nhân vật của người chơi (ActorSystem.Self.myActorCharacter); 0 nếu chưa vào màn chơi.</summary>
    public long CharacterSlot() => ActorSystem() is var system and not 0 ? system + _myActorOffset : 0;

    /// <summary>Object ActorSystem.Self; 0 nếu chưa vào màn chơi.</summary>
    public long ActorSystem() => (long)Memory.U64(_actorSystemSlot);

    /// <summary>Nhân vật của người chơi (ActorCharacter); 0 nếu chưa vào màn chơi.</summary>
    public long Character() => CharacterSlot() is var slot and not 0 ? (long)Memory.U64(slot) : 0;

    /// <summary>Xác định lại object điều khiển nhân vật (đổi khi chuyển cảnh, lên xe...).</summary>
    public void LocateActor()
    {
        var character = Character();
        var control = character != 0 ? Managed.Ptr(character, "actorControl") : 0;
        var controlClass = Managed.ClassOf(control);
        if (controlClass == 0)
        {
            if (!Memory.Alive()) throw new GameError("Game đã tắt");
            throw new GameError("Chưa thấy nhân vật trong game", true);
        }
        Control = control;
        if (controlClass == ControlClass) return;
        ControlClass = controlClass;
        L.Info($"Kiểu điều khiển: {Il2Cpp.FullName(controlClass)}");
        ControlChanged?.Invoke(controlClass);
    }

    /// <summary>Số nguyên ở <paramref name="offset"/> trong object điều khiển (1 lần đọc); object vừa đổi thì tự xác định lại.</summary>
    public int ControlValue(int offset)
    {
        var data = Control != 0 ? Memory.Read(Control, offset + 4) : null;
        if (data == null || Bin.U64(data, 0) != ControlClass)
        {
            LocateActor();
            data = Memory.Read(Control, offset + 4) ?? throw new GameError("Không đọc được trạng thái nhân vật", true);
        }
        return Bin.I32(data, offset);
    }

    /// <summary>
    /// Hệ thống con FrameWork.(name) (sysDialog, sysCollect...). Chỉ nhận FrameWork / hệ thống con ĐÚNG KIỂU: lúc game đang tải,
    /// con trỏ có thể dở dang; nhận nhầm thì mọi chức năng đọc sai. 0 nếu chưa có.
    /// </summary>
    public long System(string name)
    {
        if (_systems.TryGetValue(name, out var cached)) return cached;
        var klass = _classes.GetValueOrDefault(FrameworkClass);
        if (klass == 0) return 0;
        if (_framework == 0 || Managed.ClassOf(_framework) != klass)
        {
            var framework = (long)Memory.U64(Il2Cpp.StaticFieldPtr(klass, "_instance"));
            _framework = framework != 0 && Managed.ClassOf(framework) == klass ? framework : 0;
            _systems.Clear();
            if (_framework == 0) return 0;
        }
        var ptr = (long)Memory.U64(_framework + Il2Cpp.Field(klass, name));
        if (!Bin.IsPtr(ptr) || Il2Cpp.Names(Managed.ClassOf(ptr)).Count == 0) return 0;
        return _systems[name] = ptr;
    }

    /// <summary>Quên con trỏ hệ thống con đã nhớ (vừa thấy nó sai kiểu): lần sau đọc lại từ FrameWork.</summary>
    public void ForgetSystem(string name) => _systems.TryRemove(name, out _);

    /// <summary>
    /// Giá trị thật của số game mã hoá (PT_Encrypt.EncryptInt {key, randValue}): Storage[key]._encryptAry[Storage.key % độ dài] - randValue.
    /// 0 nếu không giải được.
    /// </summary>
    public int DecodeEncryptInt(uint key, int rand) => EncryptStored(key) is { } stored ? stored - rand : 0;

    /// <summary>Số đang lưu trong kho mã hoá cho khoá <paramref name="key"/> (giá trị thật = số này - randValue); null nếu không đọc được.</summary>
    public int? EncryptStored(uint key)
    {
        var holders = Get("encrypt-holders", () => new Dictionary<long, long>());
        lock (holders)
        {
            if (!holders.TryGetValue(key, out var holder))
            {
                holders.Clear();
                foreach (var (k, v) in Managed.DictItems((long)Memory.U64(_storageDicSlot))) holders[k] = v;
                holder = holders.GetValueOrDefault(key);
            }
            var slot = holder != 0 ? Memory.Read(holder, 0x20) : null;
            if (slot == null) return null;
            var fields = Il2Cpp.DeclaredFields(Bin.U64(slot, 0));
            var values = Bin.U64(slot, fields.GetValueOrDefault("_encryptAry", 0x10));
            var index = Bin.I16(slot, fields.GetValueOrDefault("key", 0x18));
            var head = values != 0 ? Memory.Read(values, 0x60) ?? Memory.Read(values, 0x20) : null;
            var length = head != null ? Bin.U64(head, 0x18) : 0;
            if (length is <= 0 or > 4096) return null;
            var start = (int)(0x20 + 4 * (index % length));
            var item = start + 4 <= head!.Length ? head[start..(start + 4)] : Memory.Read(values + start, 4);
            return item != null ? Bin.I32(item, 0) : null;
        }
    }
}
