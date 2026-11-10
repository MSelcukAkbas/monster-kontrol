using System.Collections.Concurrent;
using MonsterKlavye.Klavye;
using MonsterKontrol.Dchu;

namespace MonsterKlavye.Tests;

sealed class SahteDchu : IDchu
{
    public readonly ConcurrentQueue<(int Komut, uint Arguman)> Yazilanlar = new();
    public Func<bool> Hatali = () => false;

    public void Yaz(int komut, uint arguman)
    {
        if (Hatali()) throw new DllNotFoundException("sahte");
        Yazilanlar.Enqueue((komut, arguman));
    }

    public byte[] TamponOku(int komut) => new byte[256];
    public int TamsayiOku(int komut) => 0;
}

public class KlavyeKontrolTestleri
{
    static readonly Rgb Turuncu = new(255, 106, 0);
    static readonly Rgb Mavi = new(0, 0, 255);

    [Fact]
    public void Ilk_durumda_renk_ve_parlaklik_yazilir() =>
        Assert.Equal(
            [Komutlar.KlavyeRenk(255, 106, 0), Komutlar.KlavyeParlaklik(255)],
            KlavyeKontrol.Fark(null, new KlavyeDurumu(Turuncu, 100, true)));

    [Fact]
    public void Yalniz_degisen_komut_yazilir()
    {
        var a = new KlavyeDurumu(Turuncu, 80, true);
        Assert.Equal([Komutlar.KlavyeRenk(0, 0, 255)], KlavyeKontrol.Fark(a, a with { Renk = Mavi }));
        Assert.Equal([Komutlar.KlavyeParlaklik(128)], KlavyeKontrol.Fark(a, a with { ParlaklikYuzde = 50 }));
        Assert.Empty(KlavyeKontrol.Fark(a, a with { }));
    }

    [Fact]
    public void Kapatmak_parlaklik_0_yazar_ve_acmak_eski_parlakligi_geri_getirir()
    {
        var acik = new KlavyeDurumu(Turuncu, 80, true);
        var kapali = acik with { Acik = false };
        Assert.Equal([Komutlar.KlavyeParlaklik(0)], KlavyeKontrol.Fark(acik, kapali));
        Assert.Equal([Komutlar.KlavyeParlaklik(204)], KlavyeKontrol.Fark(kapali, acik));
    }

    [Theory]
    [InlineData(-5, 0)]
    [InlineData(0, 0)]
    [InlineData(50, 128)]
    [InlineData(100, 255)]
    [InlineData(150, 255)]
    public void Yuzde_donanim_parlakligina_sinirli_cevrilir(int yuzde, byte beklenen) =>
        Assert.Equal(beklenen, new KlavyeDurumu(Turuncu, yuzde, true).DonanimParlakligi);

    [Fact]
    public void Hizli_degisiklikler_birlestirilir_ve_son_durum_kazanir()
    {
        var dchu = new SahteDchu();
        using var k = new KlavyeKontrol(dchu);
        for (var ton = 0; ton < 360; ton += 3)
            k.Ayarla(new KlavyeDurumu(Rgb.TondanRgb(ton), 80, true));
        k.Ayarla(new KlavyeDurumu(Mavi, 80, true));

        Bekle(() => dchu.Yazilanlar.LastOrDefault().Arguman == Komutlar.KlavyeRenk(0, 0, 255)
                    || dchu.Yazilanlar.Any(y => y.Arguman == Komutlar.KlavyeRenk(0, 0, 255)));
        Thread.Sleep(100);

        var renkler = dchu.Yazilanlar.Where(y => (y.Arguman & 0xFF00_0000) == 0xF000_0000).ToList();
        Assert.True(renkler.Count < 121, $"birleştirme yok: {renkler.Count} renk yazıldı");
        Assert.Equal(Komutlar.KlavyeRenk(0, 0, 255), renkler[^1].Arguman);
        Assert.All(dchu.Yazilanlar, y => Assert.Equal(Komutlar.KlavyeIsigi, y.Komut));
    }

    [Fact]
    public void YenidenUygula_ayni_durumu_tekrar_yazar()
    {
        var dchu = new SahteDchu();
        using var k = new KlavyeKontrol(dchu);
        k.Ayarla(new KlavyeDurumu(Turuncu, 100, true));
        Bekle(() => dchu.Yazilanlar.Count == 2);

        k.YenidenUygula();
        Bekle(() => dchu.Yazilanlar.Count == 4);
    }

    [Fact]
    public void Hata_SonHata_ya_yazilir_ve_sonra_toparlanir()
    {
        var hatali = true;
        var dchu = new SahteDchu { Hatali = () => hatali };
        using var k = new KlavyeKontrol(dchu);

        k.Ayarla(new KlavyeDurumu(Turuncu, 100, true));
        Bekle(() => k.SonHata is not null);

        hatali = false;
        k.Ayarla(new KlavyeDurumu(Turuncu, 100, true));
        Bekle(() => k.SonHata is null && dchu.Yazilanlar.Count == 2);
    }

    static void Bekle(Func<bool> kosul)
    {
        var bitis = DateTime.UtcNow.AddSeconds(3);
        while (!kosul())
        {
            if (DateTime.UtcNow > bitis) Assert.Fail("koşul 3 sn içinde sağlanmadı");
            Thread.Sleep(5);
        }
    }
}
