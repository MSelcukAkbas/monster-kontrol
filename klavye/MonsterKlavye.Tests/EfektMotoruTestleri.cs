using MonsterKlavye.Klavye;

namespace MonsterKlavye.Tests;

public class EfektMotoruTestleri
{
    static readonly KlavyeDurumu Temel = new(new Rgb(255, 106, 0), 80, true);
    static Efekt Bul(string id) => Efektler.Hepsi.Single(e => e.Id == id);

    readonly List<KlavyeDurumu> _giden = [];
    double _sicaklik = 50;

    EfektMotoru Yeni() => new(Temel, _giden.Add, () => _sicaklik, donguyuBaslat: false);

    [Fact]
    public void Acilista_temel_durum_gonderilir()
    {
        using var m = Yeni();
        Assert.Equal([Temel], _giden);
    }

    [Fact]
    public void Nefes_parlakligi_renge_katilir_donanim_parlakligi_degismez()
    {
        using var m = Yeni();
        m.EfektAyarla(Bul("nefes"));
        _giden.Clear();
        m.Adim(2); // yarım periyot: tam parlak
        m.Adim(2); // tam periyot: karanlık
        m.Adim(1); // çeyrek: yarı parlak
        Assert.Equal([Temel, Temel with { Renk = new(0, 0, 0) }, Temel with { Renk = Temel.Renk.Olcekle(Efektler.Dalga(5, 4)) }], _giden);
        Assert.Equal(new Rgb(127, 53, 0), _giden[^1].Renk);
    }

    [Fact]
    public void Efekt_karesi_klavyeye_en_cok_bir_komut_yazar()
    {
        // Ateş hem rengi hem parlaklığı her karede değiştirir; yine de tek DLL yazması gitmeli (CPU < %1)
        var onceki = Temel;
        using var m = new EfektMotoru(Temel, d =>
        {
            Assert.True(KlavyeKontrol.Fark(onceki, d).Count <= 1);
            onceki = d;
        }, () => 50, donguyuBaslat: false);
        m.EfektAyarla(Bul("ates"));
        for (var i = 0; i < 100; i++) m.Adim(0.067);
    }

    [Fact]
    public void Hiz_fazi_olcekler_ve_ayar_degisince_faz_korunur()
    {
        using var m = Yeni();
        var nefes = Bul("nefes");
        m.EfektAyarla(nefes, new EfektAyarlari(10, Efektler.TamCark)); // 2 kat hız
        m.Adim(1);                                                      // faz 2: tam parlak
        m.EfektAyarla(nefes, new EfektAyarlari(5, Efektler.TamCark));  // aynı efekt: faz sıfırlanmaz
        m.Adim(2);                                                      // faz 4: karanlık
        Assert.Equal([Temel.Renk, new Rgb(0, 0, 0)], _giden.Skip(1).Select(d => d.Renk));
    }

    [Fact]
    public void Efekt_sirasinda_kapatinca_yalniz_kapali_durum_gider_sonra_hic_kare_gitmez()
    {
        using var m = Yeni();
        m.EfektAyarla(Bul("gokkusagi"));
        m.Adim(0.1);
        _giden.Clear();
        m.TemelAyarla(Temel with { Acik = false });
        m.Adim(0.1);
        m.Adim(0.1);
        Assert.Equal([Temel with { Acik = false }], _giden);
    }

    [Fact]
    public void Sabit_renge_donunce_temel_durum_geri_gelir()
    {
        using var m = Yeni();
        m.EfektAyarla(Bul("polis"));
        m.Adim(0.1);
        _giden.Clear();
        m.EfektAyarla(Efektler.Sabit);
        m.Adim(0.1);
        Assert.Equal([Temel], _giden);
    }

    [Fact]
    public void Sicaklik_efekti_sicakligi_okur_ve_skaladan_renk_secer()
    {
        _sicaklik = 90;
        using var m = Yeni();
        m.EfektAyarla(Efektler.Sicaklik);
        m.Adim(0.1);
        Assert.Equal(Efektler.SicaklikSkalasi.Renk(1), _giden[^1].Renk);
    }

    [Fact]
    public void Sicaklik_okunamazsa_efekt_son_degerle_surer()
    {
        using var m = new EfektMotoru(Temel, _giden.Add, () => throw new DllNotFoundException(), donguyuBaslat: false);
        m.EfektAyarla(Efektler.Sicaklik);
        m.Adim(1.5);
        Assert.Equal(Efektler.SicaklikSkalasi.Renk(0), _giden[^1].Renk); // varsayılan 50°C
    }

    [Fact]
    public void Degisti_olayi_temel_ve_efekt_degisiminde_tetiklenir()
    {
        using var m = Yeni();
        var sayi = 0;
        m.Degisti += () => sayi++;
        m.TemelAyarla(Temel with { ParlaklikYuzde = 50 });
        m.EfektAyarla(Bul("kalp"));
        Assert.Equal(2, sayi);
    }

    [Fact]
    public async Task Dongu_saniyede_yaklasik_15_kare_gonderir()
    {
        var sayi = 0;
        using var m = new EfektMotoru(Temel, _ => Interlocked.Increment(ref sayi), () => 50);
        m.EfektAyarla(Bul("gokkusagi"));
        await Task.Delay(1000);
        m.EfektAyarla(Efektler.Sabit);
        Assert.InRange(sayi, 10, 20);
    }
}
