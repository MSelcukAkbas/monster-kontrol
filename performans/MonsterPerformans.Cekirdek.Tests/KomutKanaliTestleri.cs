using System.IO.Pipes;
using MonsterPerformans.Cekirdek.Uzaktan;

namespace MonsterPerformans.Cekirdek.Tests;

public class KomutKanaliTestleri
{
    static readonly Dictionary<string, Func<object>> Komutlar = new() { ["durum"] = () => new { tamam = true, x = 1 } };

    [Theory]
    [InlineData("""{"komut":"durum"}""", """{"tamam":true,"x":1}""")]
    [InlineData("""{"komut":"fan"}""", """{"tamam":false,"hata":"bilinmeyen komut: fan"}""")]
    [InlineData("""{"x":1}""", """{"tamam":false,"hata":"'komut' alanı gerekli"}""")]
    [InlineData("bozuk", """{"tamam":false,"hata":"geçersiz JSON"}""")]
    public void Isle(string istek, string beklenen) =>
        Assert.Equal(System.Text.Json.Nodes.JsonNode.Parse(beklenen)!.ToJsonString(),
            System.Text.Json.Nodes.JsonNode.Parse(KomutKanali.Isle(istek, Komutlar))!.ToJsonString());

    [Fact]
    public async Task Boru_uzerinden_yanit_verir()
    {
        var ad = "MonsterPerformans.Test." + Guid.NewGuid().ToString("N");
        using var kanal = new KomutKanali(Komutlar, f => f(), ad);
        await using var istemci = new NamedPipeClientStream(".", ad, PipeDirection.InOut);
        await istemci.ConnectAsync(5000);
        await using var w = new StreamWriter(istemci, leaveOpen: true) { AutoFlush = true };
        using var r = new StreamReader(istemci, leaveOpen: true);
        await w.WriteLineAsync("""{"komut":"durum"}""");
        Assert.Contains("\"x\":1", await r.ReadLineAsync());
    }
}
