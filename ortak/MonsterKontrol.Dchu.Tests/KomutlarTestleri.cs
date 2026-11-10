using MonsterKontrol.Dchu;

namespace MonsterKontrol.Dchu.Tests;

public class KomutlarTestleri
{
    // Beklenen değerler prototip çıktılarından; hepsi klavyede gözle doğrulandı.
    [Theory]
    [InlineData(0, 255, 0, 0xF00000FFu)]     // yeşil
    [InlineData(255, 100, 0, 0xF000FF64u)]   // turuncu
    [InlineData(255, 0, 0, 0xF000FF00u)]     // kırmızı
    [InlineData(0, 0, 200, 0xF0C80000u)]     // CC'nin kayıtlı mavisi
    public void KlavyeRenk_B_R_G_sirasiyla_kodlanir(byte r, byte g, byte b, uint beklenen) =>
        Assert.Equal(beklenen, Komutlar.KlavyeRenk(r, g, b));

    [Theory]
    [InlineData(0, 0xF4000000u)]
    [InlineData(255, 0xF40000FFu)]
    public void KlavyeParlaklik(byte deger, uint beklenen) =>
        Assert.Equal(beklenen, Komutlar.KlavyeParlaklik(deger));

    [Fact]
    public void Bayt_little_endian() =>
        Assert.Equal(new byte[] { 0xFF, 0x00, 0x00, 0xF4 }, Komutlar.Bayt(0xF40000FFu));
}
