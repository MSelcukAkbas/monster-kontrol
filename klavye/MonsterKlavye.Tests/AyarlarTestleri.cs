using System.IO;
using MonsterKlavye.Klavye;

namespace MonsterKlavye.Tests;

public sealed class AyarlarTestleri : IDisposable
{
    readonly string _klasor = Path.Combine(Path.GetTempPath(), "MonsterKlavyeTest-" + Guid.NewGuid().ToString("N"));
    string Yol => Path.Combine(_klasor, "ayarlar.json");

    public void Dispose()
    {
        if (Directory.Exists(_klasor)) Directory.Delete(_klasor, true);
    }

    [Fact]
    public void Motor_durumu_kaydedilip_geri_okunur()
    {
        var temel = new KlavyeDurumu(new Rgb(0x2F, 0x7B, 0xFF), 45, false);
        var ates = Efektler.Hepsi.Single(e => e.Id == "ates");
        using var m = new EfektMotoru(temel, _ => { }, () => 50, donguyuBaslat: false);
        m.EfektAyarla(ates, new EfektAyarlari(8, Efektler.Neon));

        Ayarlar.Al(m, tepsiyeKucult: false).Yaz(Yol);
        var a = Ayarlar.Oku(Yol);

        Assert.Equal(temel, a.Durum);
        Assert.Same(ates, a.EtkinEfekt);
        Assert.Equal(new EfektAyarlari(8, Efektler.Neon), a.EfektAyarlari);
        Assert.False(a.TepsiyeKucult);
        Assert.False(File.Exists(Yol + ".tmp"));
    }

    [Fact]
    public void Dosya_yoksa_ya_da_bozuksa_varsayilanlar()
    {
        Assert.Equal(new Ayarlar(), Ayarlar.Oku(Yol));
        Directory.CreateDirectory(_klasor);
        File.WriteAllText(Yol, "{ bozuk");
        Assert.Equal(new Ayarlar(), Ayarlar.Oku(Yol));
    }

    [Fact]
    public void Gecersiz_degerler_sinirlanir_bilinmeyenler_varsayilana_duser()
    {
        Directory.CreateDirectory(_klasor);
        File.WriteAllText(Yol, """{ "Renk": "mavi", "Parlaklik": 900, "Efekt": "yok", "Hiz": 0, "Skala": "yok" }""");
        var a = Ayarlar.Oku(Yol);

        Assert.Equal(new KlavyeDurumu(new Rgb(0xFF, 0x6A, 0x00), 100, true), a.Durum);
        Assert.Same(Efektler.Sabit, a.EtkinEfekt);
        Assert.Equal(new EfektAyarlari(1, Efektler.TamCark), a.EfektAyarlari);
        Assert.True(a.TepsiyeKucult); // eksik alan varsayılanı alır
    }
}
