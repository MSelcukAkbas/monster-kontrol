using MonsterKlavye.Klavye;

namespace MonsterKlavye.Tests;

public class EfektTestleri
{
    static readonly Rgb Turuncu = new(255, 106, 0);

    static Kare Hesapla(string id, double faz, Skala? skala = null, double sicaklik = 50)
    {
        var e = Efektler.Hepsi.Single(x => x.Id == id);
        return e.Hesapla(new(faz, Turuncu, skala ?? e.VarsayilanSkala ?? Efektler.TamCark, sicaklik));
    }

    [Fact]
    public void Tum_efektler_parlakligi_0_ile_1_arasinda_uretir()
    {
        foreach (var e in Efektler.Hepsi)
            foreach (var skala in Efektler.Skalalar)
                for (var faz = 0.0; faz < 30; faz += 0.037)
                {
                    var k = e.Hesapla(new(faz, Turuncu, skala, 30 + faz * 3));
                    Assert.InRange(k.Parlaklik, 0, 1);
                }
    }

    [Fact]
    public void Efekt_kimlikleri_benzersiz() =>
        Assert.Equal(Efektler.Hepsi.Length, Efektler.Hepsi.Select(e => e.Id).Distinct().Count());

    [Fact]
    public void Nefes_0da_karanlik_yarim_periyotta_tam_parlak_ve_temel_renkte()
    {
        Assert.Equal(0, Hesapla("nefes", 0).Parlaklik, 6);
        Assert.Equal(1, Hesapla("nefes", 2).Parlaklik, 6);
        Assert.Equal(Turuncu, Hesapla("nefes", 1).Renk);
    }

    [Fact]
    public void Flas_periyodun_basinda_yanar_sonra_soner()
    {
        Assert.Equal(1, Hesapla("flas", 0.05).Parlaklik);
        Assert.Equal(0, Hesapla("flas", 0.3).Parlaklik);
        Assert.Equal(1, Hesapla("flas", 0.55).Parlaklik);
    }

    [Fact]
    public void Polis_skalanin_iki_ucunu_sirayla_yakar()
    {
        var s = Efektler.PolisSkalasi;
        Assert.Equal(s.Renk(0), Hesapla("polis", 0.1).Renk);
        Assert.Equal(s.Renk(1), Hesapla("polis", 0.5).Renk);
        Assert.Equal(s.Renk(0), Hesapla("polis", 0.9).Renk);
    }

    [Theory]
    [InlineData(20, 0)]
    [InlineData(50, 0)]
    [InlineData(90, 1)]
    [InlineData(105, 1)]
    public void Sicaklik_50de_bas_90da_son_renk_disinda_sinirli(double derece, double x) =>
        Assert.Equal(Efektler.SicaklikSkalasi.Renk(x), Hesapla("sicaklik", 0, sicaklik: derece).Renk);

    [Fact]
    public void Sicaklik_skalasi_yesilden_sariya_kirmiziya_gider()
    {
        var s = Efektler.SicaklikSkalasi;
        Assert.True(s.Renk(0).G == 255 && s.Renk(0).R < 100); // yeşil
        Assert.Equal(new Rgb(255, 0, 0), s.Renk(1));          // kırmızı
        Assert.True(s.Renk(0.6).R == 255 && s.Renk(0.6).G > 100); // arada sarı-turuncu, kahverengi değil
    }

    [Fact]
    public void Ates_ayni_fazda_ayni_kareyi_verir_ve_titrer()
    {
        Assert.Equal(Hesapla("ates", 3.21), Hesapla("ates", 3.21));
        var kareler = Enumerable.Range(0, 50).Select(i => Hesapla("ates", i * 0.08 + 0.01)).Distinct().Count();
        Assert.True(kareler > 40);
    }

    [Fact]
    public void Ates_hiz_ayarini_yok_sayar_kizildan_sariya_kalir()
    {
        var ates = Efektler.Hepsi.Single(e => e.Id == "ates");
        Assert.Equal(1, ates.Carpan(new EfektAyarlari(10, Efektler.Neon)));
        foreach (var k in Enumerable.Range(0, 600).Select(i => Hesapla("ates", i / 15.0)))
            Assert.True(k.Renk.R == 255 && k.Renk.B == 0 && k.Renk.G <= 165 && k.Parlaklik is >= 0.3 and <= 1, k.ToString());
    }

    [Fact]
    public void Neon_ortasi_tam_doygun_kalir()
    {
        var orta = Efektler.Neon.Renk(0.5);
        Assert.Equal(255, Math.Max(orta.R, Math.Max(orta.G, orta.B)));
        Assert.Equal(0, Math.Min(orta.R, Math.Min(orta.G, orta.B)));
    }

    [Fact]
    public void Tam_cark_adimlari_ilk_rengi_tekrarlamaz() =>
        Assert.Equal(4, Enumerable.Range(0, 4).Select(k => Efektler.TamCark.Adim(k, 4)).Distinct().Count());

    [Fact]
    public void Kisa_skala_adimlari_iki_ucu_da_kullanir()
    {
        Assert.Equal(Efektler.Neon.Renk(0), Efektler.Neon.Adim(0, 4));
        Assert.Equal(Efektler.Neon.Renk(1), Efektler.Neon.Adim(3, 4));
        Assert.Equal(Efektler.Neon.Renk(0), Efektler.Neon.Adim(4, 4));
    }

    [Fact]
    public void Rastgele_0_ile_1_arasinda() =>
        Assert.All(Enumerable.Range(-100, 1000), n => Assert.InRange(Efektler.Rastgele(n), 0, 0.9999999));
}
