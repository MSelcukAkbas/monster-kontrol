using MonsterPerformans.Cekirdek.Sensorler;

namespace MonsterPerformans.Cekirdek.Tests;

public class OlcumSonucuTestleri
{
    static readonly DateTime T = new(2026, 11, 11, 21, 0, 0);

    static Olcum O(int cpuC, int duty, double? mhz, int? gpuC, double? watt) =>
        new(T, new DchuDurumu(cpuC, 50, duty, 35, 3000, 2000, 1), [null, 100.0, mhz, null, null, null],
            new GpuDurumu(gpuC, 99, watt, 1500), new byte[32]);

    [Fact]
    public void Ozet_gecersiz_cpu_ornegini_atlar()
    {
        var s = OlcumSonucu.Ozetle("Dengeli", T, 30, [O(80, 40, 3000, 70, 40), O(90, 60, 2000, null, null), O(0, 99, 9999, 99, 99)], true, null)!;
        Assert.Equal(2, s.Ornek);
        Assert.Equal(85, s.CpuOrtC);
        Assert.Equal(90, s.CpuEnYuksekC);
        Assert.Equal(70, s.GpuEnYuksekC);
        Assert.Equal(2500, s.MhzOrt);
        Assert.Equal(50, s.CpuDutyOrt);
        Assert.Equal(40, s.GpuWattOrt);
        Assert.Equal("CPU ort. 85,0°C, en çok 90°C · 2,50 GHz · fan %50 · GPU en çok 70°C, 40 W", s.Satir());
    }

    [Fact]
    public void Gecerli_ornek_yoksa_ozet_yok() => Assert.Null(OlcumSonucu.Ozetle("X", T, 30, [O(127, 0, null, null, null)], false, null));

    [Fact]
    public void Kiyas_ayni_profil_ve_surenin_son_tam_olcumuyle()
    {
        OlcumSonucu S(string p, int sn, int dk, double c, string? kesildi = null) =>
            new(p, T.AddMinutes(dk), sn, 30, c, 90, null, 3000 + dk, 50, null, true, kesildi);
        var yeni = S("Dengeli", 30, 10, 84);
        Assert.Null(yeni.Kiyas([S("Sessiz", 30, 1, 70), S("Dengeli", 60, 2, 70)]));
        var gecmis = new[] { S("Dengeli", 30, 1, 80), S("Dengeli", 30, 2, 86), S("Dengeli", 30, 3, 60, "iptal edildi"), S("Dengeli", 30, 20, 60) };
        Assert.Equal("11.11 21:02 ölçümüne göre: CPU ort. −2,0°C, frekans +8 MHz, fan 0 puan", yeni.Kiyas(gecmis));
    }

    [Fact]
    public void Kayit_eklenir_ve_geri_okunur()
    {
        var yol = Path.Combine(Path.GetTempPath(), $"mp-olcum-{Guid.NewGuid():N}.json");
        try
        {
            var s = OlcumSonucu.Ozetle("Dengeli", T, 30, [O(80, 40, 3000, 70, 40)], true, null)!;
            OlcumSonucu.Ekle(yol, s);
            OlcumSonucu.Ekle(yol, s with { Profil = "Sessiz" });
            Assert.Equal([s, s with { Profil = "Sessiz" }], OlcumSonucu.Oku(yol));
        }
        finally { File.Delete(yol); }
    }
}
