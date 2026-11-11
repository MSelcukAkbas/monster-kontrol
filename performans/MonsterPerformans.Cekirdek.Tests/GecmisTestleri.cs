namespace MonsterPerformans.Cekirdek.Tests;

public class GecmisTestleri
{
    [Fact]
    public void Dolunca_en_eski_duser()
    {
        var g = new Gecmis<int>(3);
        Assert.Empty(g.Son(5));
        for (var i = 1; i <= 5; i++) g.Ekle(i);
        Assert.Equal(3, g.Sayi);
        Assert.Equal([3, 4, 5], g.Son(10));
        Assert.Equal([4, 5], g.Son(2));
    }

    [Theory]
    [InlineData(69, SicaklikBandi.Normal)]
    [InlineData(70, SicaklikBandi.Ilik)]
    [InlineData(84, SicaklikBandi.Ilik)]
    [InlineData(85, SicaklikBandi.Sicak)]
    [InlineData(91, SicaklikBandi.Sicak)]
    [InlineData(92, SicaklikBandi.Kritik)]
    public void Sicaklik_bantlari(int c, SicaklikBandi b) => Assert.Equal(b, Sicaklik.Band(c));
}
