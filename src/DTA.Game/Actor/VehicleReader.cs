using DTA.Game.Session;
using DTA.Runtime.Core;

namespace DTA.Game.Actor;

/// <summary>Phương tiện nhân vật đang dùng: object, motor (0 nếu không có), ghế (0 = lái), tên tài nguyên, loại (Boat / Car / Motorbike / Skateboard / Vehicle).</summary>
public sealed record Vehicle(long Instance, long Motor, int Seat, string Asset, string Kind)
{
    public bool IsBoat => Kind == "Boat";
    public bool IsDriver => Seat == 0;
}

/// <summary>Đọc phương tiện đang cưỡi / lái (ActorCharacter._attachVehicle hoặc ActorSystem.loadVehicleList theo chủ) - dùng chung theo phiên.</summary>
public sealed class VehicleReader(GameSession session)
{
    private static readonly (string Kind, string[] Words)[] Kinds =
    [
        ("Boat", ["boat", "ship", "water", "canoe", "yacht"]), ("Car", ["car", "auto", "jeep", "truck", "bus"]),
        ("Motorbike", ["bike", "motor", "scooter"]), ("Skateboard", ["board", "skate"]),
    ];

    /// <summary>Phương tiện đang dùng; null nếu đi bộ.</summary>
    public Vehicle? Current() => session.Optional(Read, "phương tiện");

    private Vehicle? Read()
    {
        var m = session.Managed;
        var character = session.Character();
        var klass = m.ClassOf(character);
        if (klass == 0) return null;
        var meta = session.Memory.Read(character + session.Il2Cpp.Field(klass, "rideVehicleMode"), 8);
        var (riding, seat) = meta != null ? (meta[0] != 0, Bin.I32(meta, 4)) : (false, -1);
        var vehicle = m.Ptr(character, "_attachVehicle");
        if (!Bin.IsPtr(vehicle) && (riding || seat >= 0)) vehicle = FindOwned(character);
        if (!Bin.IsPtr(vehicle) || m.ClassOf(vehicle) == 0) return null;
        var kunit = m.Ptr(m.Ptr(vehicle, "actorControl"), "kunit");
        var motor = Bin.IsPtr(kunit) ? m.Ptr(kunit, "Motor") : 0;
        var assetPtr = m.Ptr(vehicle, "assetName");
        var asset = assetPtr != 0 ? session.Memory.Strings([assetPtr], 64).GetValueOrDefault(assetPtr, "") : "";
        var lower = asset.ToLowerInvariant();
        var kind = Kinds.FirstOrDefault(k => k.Words.Any(lower.Contains)).Kind ?? "Vehicle";
        return new Vehicle(vehicle, Bin.IsPtr(motor) ? motor : 0, seat, asset, kind);
    }

    /// <summary>Phương tiện trong ActorSystem.loadVehicleList có OwnerActorUID = SUID nhân vật.</summary>
    private long FindOwned(long character)
    {
        var m = session.Managed;
        var system = session.ActorSystem();
        var suid = session.Memory.Read(character + session.Il2Cpp.Field(m.ClassOf(character), "SUID"), 8);
        if (system == 0 || suid == null) return 0;
        foreach (var vehicle in m.ListItems(m.Ptr(system, "loadVehicleList")).Where(Bin.IsPtr))
        {
            var owner = session.Memory.Read(vehicle + session.Il2Cpp.Field(m.ClassOf(vehicle), "OwnerActorUID"), 8);
            if (owner != null && owner.AsSpan().SequenceEqual(suid)) return vehicle;
        }
        return 0;
    }
}
