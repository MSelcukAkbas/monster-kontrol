using MonsterKontrol.Dchu;
using MonsterPerformans.Cekirdek.Fan;

namespace MonsterPerformans.Cekirdek.Tests;

public class FanTestleri
{
    static readonly FanEgrisi Cpu = new(53, 98, 80, 99), Gpu = new(61, 79, 80, 80);

    // Gerçek tampon 13 (11.11.2026, CC eğrisi yüklüyken); GPU 4. noktası 99°C
    static byte[] T13() => Doldur(
        [0, 0, 0, 0, 200, 0, 0, 200, 0, 0, 200, 0, 2, 0, 2, 6, 40, 89, 53, 250, 80, 252, 100, 255,
         40, 89, 61, 201, 80, 204, 99, 255, 0, 0, 100, 255, 100, 255, 0, 0, 0, 0, 0, 1]);

    static byte[] Doldur(byte[] b) { var t = new byte[256]; b.CopyTo(t, 0); return t; }

    [Fact]
    public void Komut14_prototipin_urettigi_baytlarla_ayni()
    {
        // prototip/testE_egri14.py komut14(fanlar(sayfa4())) çıktısındaki sıfır olmayan baytlar
        (int i, byte v)[] beklenen =
        [
            (2, 53), (3, 250), (4, 80), (5, 252), (6, 61), (7, 201), (8, 80), (9, 204), (10, 100), (11, 255),
            (12, 100), (13, 255), (15, 198), (17, 2), (19, 2), (21, 85), (23, 2), (25, 41), (27, 41),
        ];
        var b = new byte[256];
        foreach (var (i, v) in beklenen) b[i] = v;
        Assert.Equal(b, FanKontrol.Komut14(Cpu, Gpu));
    }

    [Fact]
    public void Tampon13_cozulur_ve_dogrulanir()
    {
        Assert.True(FanKontrol.Dogrula(T13(), Cpu, Gpu));
        Assert.False(FanKontrol.Dogrula(T13(), Cpu with { T3 = 81 }, Gpu));
        Assert.False(FanKontrol.Dogrula(T13(), Cpu, Gpu with { D2 = 80 }));
        Assert.Equal((Cpu, Gpu), new FanKontrol(new SahteDchu(T13())).EcEgrisi());
    }

    [Theory]
    [InlineData(100)]
    [InlineData(0)]
    [InlineData(35)]
    [InlineData(255)]
    public void Duty_ve_yuzde_tutarli(int duty)
    {
        var y = FanEgrisi.Yuzde((byte)duty);
        Assert.InRange(Math.Abs(FanEgrisi.Duty(y) - duty), 0, 1);
        Assert.Equal(y, FanEgrisi.Yuzde(FanEgrisi.Duty(y)));
    }

    [Theory]
    [InlineData(40, 50, 80, 60, 100)] // t2 = t1
    [InlineData(60, 50, 60, 60, 100)] // t2 = t3
    [InlineData(60, 50, 99, 60, 99)]  // t3 GPU'nun canlı 4. noktasına eşit
    [InlineData(60, 34, 80, 60, 100)] // taban %35'in altında
    [InlineData(60, 70, 80, 60, 100)] // fan % azalıyor
    [InlineData(60, 50, 80, 101, 100)]
    public void Gecersiz_egri_reddedilir(int t2, int d2, int t3, int d3, int t4) =>
        Assert.NotNull(new FanEgrisi(t2, d2, t3, d3).Hata(t4));

    [Fact]
    public void Gecerli_egri_kabul_edilir() => Assert.Null(Cpu.Hata(100));

    [Fact]
    public void Dogrulanan_egride_ozel_moda_gecer()
    {
        var d = new SahteDchu(T13()) { Yansit = true };
        var egri = new FanEgrisi(60, 50, 85, 80);
        Assert.True(new FanKontrol(d, TimeSpan.Zero).OzelEgriUygula(egri, egri));
        Assert.Equal([(14, -1L), (0x79, 0x0100_0006L)], d.Yazilanlar);
    }

    [Fact]
    public void Dogrulanamayan_egride_guvenli_duruma_doner()
    {
        var d = new SahteDchu(T13());
        var egri = new FanEgrisi(60, 50, 85, 80);
        Assert.False(new FanKontrol(d, TimeSpan.Zero).OzelEgriUygula(egri, egri));
        Assert.Equal([(14, -1L), (0x79, 0x0100_0000L), (0x69, 0x0FL)], d.Yazilanlar);
    }

    [Fact]
    public void Yazma_hatasinda_guvenli_duruma_doner()
    {
        var d = new SahteDchu(T13()) { YazmaHatasi = true };
        var egri = new FanEgrisi(60, 50, 85, 80);
        Assert.Throws<InvalidOperationException>(() => new FanKontrol(d, TimeSpan.Zero).OzelEgriUygula(egri, egri));
        Assert.Equal([(0x79, 0x0100_0000L), (0x69, 0x0FL)], d.Yazilanlar);
    }

    [Fact]
    public void Gecersiz_egri_hicbir_sey_yazmaz()
    {
        var d = new SahteDchu(T13()) { Yansit = true };
        // GPU 4. noktası 99°C: t3 = 99 reddedilmeli
        Assert.Throws<ArgumentException>(() =>
            new FanKontrol(d, TimeSpan.Zero).OzelEgriUygula(Cpu, new FanEgrisi(61, 79, 99, 80)));
        Assert.Empty(d.Yazilanlar);
    }

    /// <summary>Yansit: komut 14 yazılınca 2. ve 3. noktaları tampon 13'e koyar (EC gibi).</summary>
    sealed class SahteDchu(byte[] t13) : IDchu
    {
        public bool Yansit, YazmaHatasi;
        public List<(int Komut, long Arguman)> Yazilanlar { get; } = [];

        public void Yaz(int komut, uint arguman) => Yazilanlar.Add((komut, arguman));

        public void TamponYaz(int komut, byte[] veri)
        {
            if (YazmaHatasi) throw new InvalidOperationException("DLL hatası");
            Yazilanlar.Add((komut, -1));
            if (Yansit && komut == 14)
            {
                veri.AsSpan(2, 4).CopyTo(t13.AsSpan(18));
                veri.AsSpan(6, 4).CopyTo(t13.AsSpan(26));
            }
        }

        public byte[] TamponOku(int komut) => komut == 13 ? (byte[])t13.Clone() : new byte[256];
        public int TamsayiOku(int komut) => 0;
    }
}
