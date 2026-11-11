using MonsterPerformans.Cekirdek.Fan;
using MonsterPerformans.Cekirdek.Guc;
using static MonsterPerformans.Cekirdek.Tests.FanTestleri;

namespace MonsterPerformans.Cekirdek.Tests;

public class ProfilTestleri
{
    static readonly Guid Temel = WindowsGucPlani.Dengeli;
    static readonly FanEgrisi Egri = new(60, 50, 85, 80);

    static (ProfilUygulayici U, SahteDchu D, SahtePlan P) Kur(bool yansit = true)
    {
        var d = new SahteDchu(T13()) { Yansit = yansit };
        var p = new SahtePlan();
        return (new ProfilUygulayici(d, p, new FanKontrol(d, TimeSpan.Zero)), d, p);
    }

    [Theory]
    [InlineData(100, 0u)]
    [InlineData(70, 2870u)]
    [InlineData(1, 41u)]
    public void Frekans_yuzdesi_mhz_olur(int yuzde, uint mhz) => Assert.Equal(mhz, ProfilUygulayici.FrekansMhz(yuzde));

    [Fact]
    public void Profil_kopya_plana_yazilir_ve_dogrulanir()
    {
        var (u, d, p) = Kur();
        var s = u.Uygula(new("Test", GucSeviyesi.Sessiz, Temel, 70, false, FanModu.Ozel, Egri, Egri));

        Assert.All(s, a => Assert.NotEqual(AdimDurumu.Basarisiz, a.Durum));
        Assert.Equal([AdimDurumu.Yazildi, AdimDurumu.Dogrulandi, AdimDurumu.Dogrulandi], s.Select(a => a.Durum));
        var kopya = WindowsGucPlani.KopyaGuid(Temel);
        Assert.Equal(kopya, p.Etkin);
        Assert.Equal((2870u, 2870u), p.IslemciAyariOku(kopya, WindowsGucPlani.FrekansSiniri));
        Assert.Equal((0u, 0u), p.IslemciAyariOku(kopya, WindowsGucPlani.TurboModu));
        Assert.DoesNotContain(p.Degerler.Keys, k => k.Plan == Temel); // temel plana dokunulmadı
        // güç → (plan) → fan: eğri Sessiz'de yazılır, güç yeniden yazılır, mod 6 en son (güç yazması özel modu bozar)
        Assert.Equal([(0x79, 0x1900_0000L), (0x79, 0x1900_0000L), (14, -1L), (0x79, 0x1900_0000L), (0x79, 0x0100_0006L)], d.Yazilanlar);
    }

    [Fact]
    public void Turbo_acik_2_yazar() =>
        Assert.Equal((2u, 2u), Uygulanmis(new("T", GucSeviyesi.Eglence, Temel, 100, true, FanModu.Maksimum))
            .IslemciAyariOku(WindowsGucPlani.KopyaGuid(Temel), WindowsGucPlani.TurboModu));

    static SahtePlan Uygulanmis(Profil pr) { var (u, _, p) = Kur(); u.Uygula(pr); return p; }

    [Fact]
    public void Gecersiz_frekans_plani_atlar_digerleri_uygulanir()
    {
        var (u, d, p) = Kur();
        var s = u.Uygula(new("T", GucSeviyesi.Performans, Temel, 0, true, FanModu.Maksimum));
        Assert.Equal([AdimDurumu.Yazildi, AdimDurumu.Basarisiz, AdimDurumu.Yazildi], s.Select(a => a.Durum));
        Assert.Empty(p.Degerler);
        Assert.Equal([(0x79, 0x1900_0002L), (0x79, 0x0100_0001L)], d.Yazilanlar);
    }

    [Fact]
    public void Dogrulanamayan_egride_fan_basarisiz_ve_guvenli()
    {
        var (u, d, _) = Kur(yansit: false);
        var s = u.Uygula(new("T", GucSeviyesi.Eglence, Temel, 100, true, FanModu.Ozel, Egri, Egri));
        Assert.Equal(AdimDurumu.Basarisiz, s[2].Durum);
        Assert.Equal([(0x79, 0x1900_0000L), (0x79, 0x0100_0000L), (0x69, 0x0FL), (0x79, 0x1900_0003L)], d.Yazilanlar[^4..]);
        Assert.DoesNotContain((0x79, 0x0100_0006L), d.Yazilanlar);
    }

    [Fact]
    public void Dogrulama_politikasi_guvenli_durum_hepsini_geri_alir()
    {
        var (u, d, p) = Kur(yansit: false);
        var s = u.Uygula(new("T", GucSeviyesi.Performans, Temel, 70, true, FanModu.Ozel, Egri, Egri), DogrulamaPolitikasi.GuvenliDurum);
        Assert.Equal(6, s.Count);
        Assert.Equal(WindowsGucPlani.Dengeli, p.Etkin);
        Assert.Equal((0x79, 0x1900_0003L), d.Yazilanlar[^1]);
        Assert.Equal(2, d.Yazilanlar.Count(y => y.Komut == 14)); // eğri bir kez daha denendi
    }

    [Fact]
    public void Gecersiz_egride_fan_guvenli_duruma_doner()
    {
        var (u, d, _) = Kur();
        var s = u.Uygula(new("T", GucSeviyesi.Eglence, Temel, 100, true, FanModu.Ozel)); // varsayılan eğri geçersiz
        Assert.Equal(AdimDurumu.Basarisiz, s[2].Durum);
        Assert.Equal([(0x79, 0x1900_0003L), (0x79, 0x1900_0000L), (0x79, 0x1900_0000L), (0x79, 0x0100_0000L), (0x69, 0x0FL), (0x79, 0x1900_0003L)], d.Yazilanlar);
    }

    // Eğlence'deyken mod 0 + 0x69 fanları %95'e fırlatıyor (Gunluk 11.11 18:58:22): otomatiğe hep Sessiz'de geçilir (güvenli durum: ProfilDeposuTestleri)
    [Fact]
    public void Fan_otomatige_guc_sessizdeyken_alinir()
    {
        var (u, d, _) = Kur();
        u.Uygula(new("T", GucSeviyesi.Eglence, Temel, 100, true, FanModu.Otomatik));
        Assert.Equal([(0x79, 0x1900_0003L), (0x79, 0x1900_0000L), (0x79, 0x0100_0000L), (0x69, 0x0FL), (0x79, 0x1900_0003L)], d.Yazilanlar);
    }

    internal sealed class SahtePlan : IGucPlani
    {
        public Dictionary<(Guid Plan, Guid Ayar), (uint, uint)> Degerler { get; } = [];
        public Guid KopyaHazirla(Guid temel) => WindowsGucPlani.KopyaGuid(temel);
        public void IslemciAyariYaz(Guid plan, Guid ayar, uint deger) => Degerler[(plan, ayar)] = (deger, deger);
        public (uint Ac, uint Dc) IslemciAyariOku(Guid plan, Guid ayar) => Degerler[(plan, ayar)];
        public Guid Etkin { get; private set; } = WindowsGucPlani.Dengeli;
        public void Etkinlestir(Guid plan) => Etkin = plan;
    }
}
