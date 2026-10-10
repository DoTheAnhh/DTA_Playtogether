using System.Buffers.Binary;

namespace DTA.Runtime.Core;

/// <summary>Đọc số little-endian từ mảng byte đọc ra từ bộ nhớ game (hàm chung cho mọi tầng).</summary>
public static class Bin
{
    public static long U64(byte[] data, int at) => (long)BinaryPrimitives.ReadUInt64LittleEndian(data.AsSpan(at));
    public static int I32(byte[] data, int at) => BinaryPrimitives.ReadInt32LittleEndian(data.AsSpan(at));
    public static uint U32(byte[] data, int at) => BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(at));
    public static int U16(byte[] data, int at) => BinaryPrimitives.ReadUInt16LittleEndian(data.AsSpan(at));
    public static short I16(byte[] data, int at) => BinaryPrimitives.ReadInt16LittleEndian(data.AsSpan(at));
    public static float F32(byte[] data, int at) => BinaryPrimitives.ReadSingleLittleEndian(data.AsSpan(at));
    public static (float X, float Y, float Z) Vec3(byte[] data, int at) => (F32(data, at), F32(data, at + 4), F32(data, at + 8));

    public static byte[] Pack(long value) => BitConverter.GetBytes(value);
    public static byte[] Pack(int value) => BitConverter.GetBytes(value);
    public static byte[] Pack(float value) => BitConverter.GetBytes(value);
    public static byte[] Pack(params float[] values) => values.SelectMany(BitConverter.GetBytes).ToArray();

    /// <summary>Con trỏ hợp lệ trong vùng nhớ game (loại 0 và địa chỉ thấp rác).</summary>
    public static bool IsPtr(long value) => value >= 0x10000000 && (value & 7) == 0;
}
