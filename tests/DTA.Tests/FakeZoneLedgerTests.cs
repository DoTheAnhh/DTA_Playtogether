using DTA.Features.Fishing;
using Xunit;

namespace DTA.Tests;

/// <summary>Kiểm thử sổ ghi vùng câu giả: bật, đổi vùng, tắt, object chết, ID không hợp lệ.</summary>
public class FakeZoneLedgerTests
{
    private static Dictionary<long, uint> Live(params (long Info, uint Id)[] cells) => cells.ToDictionary(c => c.Info, c => c.Id);

    [Fact]
    public void Bat_gia_vung_ghi_moi_o_khac_vung_gia()
    {
        var ledger = new FakeZoneLedger();
        var writes = ledger.Apply(Live((1, 1002), (2, 1003), (3, 3011)), 3011);
        Assert.Equal([(1L, 3011u), (2L, 3011u)], writes);
        Assert.True(ledger.Active);
    }

    [Fact]
    public void Doi_vung_gia_van_giu_id_goc_that()
    {
        var ledger = new FakeZoneLedger();
        ledger.Apply(Live((1, 1002)), 3011);
        var writes = ledger.Apply(Live((1, 3011)), 501);
        Assert.Equal([(1L, 501u)], writes);
        Assert.Equal([(1L, 1002u)], ledger.Restore(Live((1, 501))));
    }

    [Fact]
    public void Tat_gia_vung_tra_id_goc_va_xoa_so()
    {
        var ledger = new FakeZoneLedger();
        ledger.Apply(Live((1, 1002), (2, 2002)), 3011);
        var writes = ledger.Restore(Live((1, 3011), (2, 3011)));
        Assert.Equal([(1L, 1002u), (2L, 2002u)], writes.OrderBy(w => w.Info));
        Assert.False(ledger.Active);
        Assert.Empty(ledger.Restore(Live((1, 3011))));
    }

    [Fact]
    public void Object_da_chet_khong_bao_gio_bi_ghi()
    {
        var ledger = new FakeZoneLedger();
        ledger.Apply(Live((1, 1002), (2, 2002)), 3011);
        Assert.Equal([(2L, 2002u)], ledger.Restore(Live((2, 3011))));
        Assert.Empty(new FakeZoneLedger().Apply(Live(), 3011));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    [InlineData(FakeZoneLedger.MaxZoneId)]
    public void Id_khong_hop_le_khong_ghi_gi(int zone)
    {
        var ledger = new FakeZoneLedger();
        Assert.Empty(ledger.Apply(Live((1, 1002)), zone));
        Assert.False(ledger.Active);
    }

    [Fact]
    public void Id_goc_biet_truoc_luc_quet_duoc_uu_tien()
    {
        var ledger = new FakeZoneLedger();
        ledger.Apply(Live((1, 3011)), 501, new Dictionary<long, uint> { [1] = 1002 });
        Assert.Equal(1002u, ledger.Original(1, 501));
    }

    [Fact]
    public void Doi_canh_quen_so_khong_ghi_tra()
    {
        var ledger = new FakeZoneLedger();
        ledger.Apply(Live((1, 1002)), 3011);
        ledger.Forget();
        Assert.False(ledger.Active);
        Assert.Empty(ledger.Restore(Live((1, 3011))));
    }
}
