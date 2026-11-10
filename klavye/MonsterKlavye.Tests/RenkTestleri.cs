using MonsterKlavye.Klavye;

namespace MonsterKlavye.Tests;

public class RenkTestleri
{
    [Theory]
    [InlineData(0, 255, 0, 0)]
    [InlineData(60, 255, 255, 0)]
    [InlineData(120, 0, 255, 0)]
    [InlineData(180, 0, 255, 255)]
    [InlineData(240, 0, 0, 255)]
    [InlineData(300, 255, 0, 255)]
    [InlineData(360, 255, 0, 0)]
    [InlineData(-60, 255, 0, 255)]
    public void TondanRgb(double ton, byte r, byte g, byte b) =>
        Assert.Equal(new Rgb(r, g, b), Rgb.TondanRgb(ton));

    [Theory]
    [InlineData(0)]
    [InlineData(25)]
    [InlineData(140)]
    [InlineData(218)]
    [InlineData(330)]
    public void Ton_gidis_donus(double ton) =>
        Assert.InRange(Rgb.TondanRgb(ton).Ton, ton - 1, ton + 1);

    [Theory]
    [InlineData("#FF6A00", 255, 106, 0)]
    [InlineData("ff6a00", 255, 106, 0)]
    [InlineData("  #00d5e0 ", 0, 213, 224)]
    public void HexCoz_gecerli(string metin, byte r, byte g, byte b) =>
        Assert.Equal(new Rgb(r, g, b), Rgb.HexCoz(metin));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("#FF6A0")]
    [InlineData("#GG0000")]
    [InlineData("#FF6A0000")]
    public void HexCoz_gecersiz_null(string? metin) =>
        Assert.Null(Rgb.HexCoz(metin));

    [Fact]
    public void Hex_bicimi() => Assert.Equal("#FF6A00", new Rgb(255, 106, 0).Hex);
}
