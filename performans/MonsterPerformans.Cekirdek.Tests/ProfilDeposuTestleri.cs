using MonsterPerformans.Cekirdek.Fan;
using MonsterPerformans.Cekirdek.Guc;
using static MonsterPerformans.Cekirdek.Tests.FanTestleri;

namespace MonsterPerformans.Cekirdek.Tests;

public class ProfilDeposuTestleri : IDisposable
{
    readonly string _yol = Path.Combine(Path.GetTempPath(), $"mp-test-{Guid.NewGuid():N}.json");

    public void Dispose() => File.Delete(_yol);

    [Fact]
    public void Kayit_geri_okunur()
    {
        var a = new PerformansAyarlari();
        var k = a.Kopyala(HazirProfiller.Performans);
        a.TepsiProfili = k.Ad;
        a.Yaz(_yol);

        var b = PerformansAyarlari.Oku(_yol);
        Assert.Equal([k], b.Profiller);
        Assert.Equal("Performans kopyası", b.TepsiProfili);
        Assert.Equal(HazirEgriler.Dengeli, b.Profiller[0].CpuEgrisi); // hazır profilin boş eğrisi doldurulur
    }

    [Theory]
    [InlineData("{ bozuk")]
    [InlineData("""{"Profiller":[{"Ad":"X","Guc":"Uydurma"}]}""")]
    public void Bozuk_dosya_bos_ayar_verir(string icerik)
    {
        File.WriteAllText(_yol, icerik);
        Assert.Empty(PerformansAyarlari.Oku(_yol).Profiller);
    }

    [Fact]
    public void Elle_duzenlenmis_degerler_sinirlanir()
    {
        File.WriteAllText(_yol, """
            {"Profiller":[
              {"Ad":"sessiz","Guc":"Sessiz","WindowsPlani":"381b4222-f694-41f0-9685-ff5bb260df2e","FrekansYuzde":50,"Turbo":false,"Fan":"Otomatik"},
              {"Ad":"A","Guc":"Sessiz","WindowsPlani":"381b4222-f694-41f0-9685-ff5bb260df2e","FrekansYuzde":500,"Turbo":false,"Fan":"Otomatik"},
              {"Ad":"A","Guc":"Eglence","WindowsPlani":"381b4222-f694-41f0-9685-ff5bb260df2e","FrekansYuzde":0,"Turbo":true,"Fan":"Otomatik"}
            ],"TepsiProfili":"Yok"}
            """);
        var a = PerformansAyarlari.Oku(_yol);
        var p = Assert.Single(a.Profiller); // hazır adı ve yinelenen ad düşer
        Assert.Equal(100, p.FrekansYuzde);
        Assert.Null(a.TepsiProfili);
    }

    [Fact]
    public void Kopya_adi_tekil_ve_ad_degisince_tepsi_tasinir()
    {
        var a = new PerformansAyarlari();
        var k1 = a.Kopyala(HazirProfiller.Sessiz);
        var k2 = a.Kopyala(HazirProfiller.Sessiz);
        Assert.Equal("Sessiz kopyası 2", k2.Ad);

        a.TepsiProfili = k1.Ad;
        a.Degistir(k1, k1 with { Ad = "Oyun" });
        Assert.Equal("Oyun", a.TepsiProfili);
        Assert.Throws<ArgumentException>(() => a.Degistir(k2, k2 with { Ad = "Oyun" }));
        Assert.Throws<ArgumentException>(() => a.Degistir(k2, k2 with { Ad = "dengeli" }));

        a.Sil(a.Profiller[0]);
        Assert.Null(a.TepsiProfili);
        Assert.True(HazirProfiller.Kilitli(HazirProfiller.Dengeli));
        Assert.False(HazirProfiller.Kilitli(k2));
    }

    [Fact]
    public void Hazir_egriler_iki_fan_icin_gecerli() =>
        Assert.All(HazirEgriler.Hepsi, h => { Assert.Null(h.Egri.Hata(100)); Assert.Null(h.Egri.Hata(99)); });

    [Fact]
    public void Guvenli_durum_sirasi()
    {
        var d = new SahteDchu(T13());
        var p = new ProfilTestleri.SahtePlan();
        p.Etkinlestir(WindowsGucPlani.KopyaGuid(WindowsGucPlani.Dengeli));
        var s = new ProfilUygulayici(d, p, new FanKontrol(d, TimeSpan.Zero)).GuvenliDurum();

        Assert.Equal([AdimDurumu.Yazildi, AdimDurumu.Yazildi, AdimDurumu.Dogrulandi], s.Select(a => a.Durum));
        Assert.Equal([(0x79, 0x1900_0000L), (0x79, 0x0100_0000L), (0x69, 0x0FL), (0x79, 0x1900_0003L)], d.Yazilanlar);
        Assert.Equal(WindowsGucPlani.Dengeli, p.Etkin);
    }

    [Fact]
    public void Plan_listesi_dengeliyi_icerir_kopyalari_icermez() // salt okuma
    {
        var l = WindowsGucPlani.Planlar();
        Assert.Contains(l, x => x.Guid == WindowsGucPlani.Dengeli);
        Assert.DoesNotContain(l, x => x.Ad.StartsWith(WindowsGucPlani.KopyaOnEki));
    }

    [Fact]
    public void Eski_dosya_yeni_ayarlari_varsayilanla_acar()
    {
        File.WriteAllText(_yol, """{"Profiller":[],"TepsiProfili":null}""");
        var a = PerformansAyarlari.Oku(_yol);
        Assert.Equal(92, a.AcilEsik);
        Assert.False(a.YenidenUygula);
        Assert.True(a.KayitTut);
        Assert.Equal(DogrulamaPolitikasi.FanOtomatik, a.DogrulamaHatasi);
        Assert.Equal(PerformansAyarlari.VarsayilanKayitKlasoru, a.Kayit);
    }

    [Fact]
    public void Yeni_ayarlar_sinirlanir_ve_gecersiz_son_profil_silinir()
    {
        File.WriteAllText(_yol, """{"AcilEsik":80,"SonProfil":"Yok","DogrulamaHatasi":7}""");
        var a = PerformansAyarlari.Oku(_yol);
        Assert.Equal(92, a.AcilEsik);
        Assert.Null(a.SonProfil);
        Assert.Equal(DogrulamaPolitikasi.FanOtomatik, a.DogrulamaHatasi);
    }

    [Fact]
    public void Son_profil_ad_degisince_tasinir_silinince_bosalir()
    {
        var a = new PerformansAyarlari();
        var k = a.Kopyala(HazirProfiller.Dengeli);
        a.SonProfil = k.Ad;
        var y = k with { Ad = "Yeni" };
        a.Degistir(k, y);
        Assert.Equal("Yeni", a.SonProfil);
        Assert.Same(y, a.Bul("Yeni"));
        a.Sil(y);
        Assert.Null(a.SonProfil);
        Assert.Same(HazirProfiller.Sessiz, a.Bul("Sessiz"));
    }
}
