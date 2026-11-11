using System.Text.Json;
using MonsterKlavye.Klavye;
using MonsterKlavye.Uzaktan;

namespace MonsterKlavye.Tests;

public class KomutKanaliTestleri
{
    static readonly KlavyeDurumu Temel = new(new Rgb(255, 106, 0), 80, true);
    readonly EfektMotoru _m = new(Temel, _ => { }, () => 50, donguyuBaslat: false);

    JsonElement Isle(string istek) => JsonDocument.Parse(KomutKanali.Isle(istek, _m)).RootElement;

    static void Hatali(JsonElement y) => Assert.False(y.GetProperty("tamam").GetBoolean());

    [Fact]
    public void Renk_isigi_acar_ve_skalali_efekti_sabite_dondurur()
    {
        _m.TemelAyarla(Temel with { Acik = false });
        _m.EfektAyarla(Efektler.Hepsi.Single(e => e.Id == "ates"));
        var y = Isle("""{"komut":"renk","deger":"#00ff80"}""");
        Assert.Equal("#00FF80", y.GetProperty("renk").GetString());
        Assert.True(_m.Temel.Acik);
        Assert.Same(Efektler.Sabit, _m.Efekt);
    }

    [Fact]
    public void Efekt_hiz_ve_skala_ile_secilir()
    {
        var y = Isle("""{"komut":"efekt","id":"gokkusagi","hiz":8,"skala":"Neon"}""");
        Assert.Equal("gokkusagi", y.GetProperty("efekt").GetString());
        Assert.Equal(new EfektAyarlari(8, Efektler.Neon), _m.Ayarlar);
    }

    [Fact]
    public void Parlaklik_ve_isik_ayarlanir()
    {
        Isle("""{"komut":"parlaklik","deger":30}""");
        Isle("""{"komut":"isik","acik":false}""");
        Assert.Equal(Temel with { ParlaklikYuzde = 30, Acik = false }, _m.Temel);
    }

    [Theory]
    [InlineData("bozuk")]
    [InlineData("[]")]
    [InlineData("""{"komut":"fan","deger":100}""")]
    [InlineData("""{"komut":"renk","deger":"kırmızı"}""")]
    [InlineData("""{"komut":"parlaklik","deger":101}""")]
    [InlineData("""{"komut":"parlaklik","deger":"50"}""")]
    [InlineData("""{"komut":"isik","acik":1}""")]
    [InlineData("""{"komut":"efekt","id":"yok"}""")]
    [InlineData("""{"komut":"efekt","id":"nefes","hiz":0}""")]
    [InlineData("""{"komut":"uyari","kez":0}""")]
    [InlineData("""{"komut":"uyari","renk":"red"}""")]
    public void Gecersiz_istek_reddedilir_durum_degismez(string istek)
    {
        Hatali(Isle(istek));
        Assert.Equal(Temel, _m.Temel);
        Assert.Same(Efektler.Sabit, _m.Efekt);
    }

    [Fact]
    public void Efektler_listesi_tum_efektleri_verir() =>
        Assert.Equal(Efektler.Hepsi.Length, Isle("""{"komut":"efektler"}""").GetProperty("efektler").GetArrayLength());
}
