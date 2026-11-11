using System.ComponentModel;
using System.IO.Pipes;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;

// Claude gibi MCP istemcileri için klavye araçları. Klavyeye kendisi yazmaz: çalışan Monster Klavye'ye
// yerel kanaldan komut iletir (durumun tek sahibi uygulama; ayrıca fan / güç burada hiç yok).
var builder = Host.CreateApplicationBuilder(args);
builder.Logging.AddConsole(o => o.LogToStandardErrorThreshold = LogLevel.Trace); // stdout MCP'ye ait
builder.Services.AddMcpServer().WithStdioServerTransport().WithTools<KlavyeAraclari>();
await builder.Build().RunAsync();

[McpServerToolType]
public class KlavyeAraclari
{
    const string BoruAdi = "MonsterKlavye.Komut"; // uygulamadaki KomutKanali.BoruAdi ile aynı

    [McpServerTool(Name = "durum", ReadOnly = true), Description(
        "Monster Klavye'nin klavyeye en son yazdığı renk, parlaklık, açık/kapalı ve efekt. Donanımdan geri okunamaz: " +
        "kullanıcı ışığı Fn tuşuyla kapattıysa burada yine 'acik: true' görünür.")]
    public static Task<string> Durum() => Gonder(new { komut = "durum" });

    [McpServerTool(Name = "efektler", ReadOnly = true), Description("Seçilebilen efektler (id, ad, açıklama, skalalı mı) ve skala adları.")]
    public static Task<string> EfektListesi() => Gonder(new { komut = "efektler" });

    [McpServerTool(Name = "renk_ayarla"), Description("Klavyeyi sabit bir renge ayarlar ve ışığı açar. Skalalı efekt çalışıyorsa sabit renge geçer.")]
    public static Task<string> Renk([Description("#RRGGBB, ör. #FF6A00")] string renk) => Gonder(new { komut = "renk", deger = renk });

    [McpServerTool(Name = "parlaklik_ayarla"), Description("Klavye parlaklığı (yüzde).")]
    public static Task<string> Parlaklik([Description("0–100")] int yuzde) => Gonder(new { komut = "parlaklik", deger = yuzde });

    [McpServerTool(Name = "isik"), Description("Klavye ışığını açar ya da kapatır.")]
    public static Task<string> Isik([Description("true = aç, false = kapat")] bool acik) => Gonder(new { komut = "isik", acik });

    [McpServerTool(Name = "uyari"), Description(
        "Kullanıcıyı uyarmak için klavyeyi tam parlaklıkta birkaç kez yakıp söndürür (her biri 0,5 sn), sonra önceki " +
        "renk / efekt / açık-kapalı duruma kendiliğinden döner. Işık kapalıyken de çalışır; ayarları değiştirmez.")]
    public static Task<string> Uyari(
        [Description("#RRGGBB; verilmezse kırmızı")] string? renk = null,
        [Description("Yanıp sönme sayısı 1–20; verilmezse 3")] int? kez = null) =>
        Gonder(new Dictionary<string, object?> { ["komut"] = "uyari", ["renk"] = renk, ["kez"] = kez }
            .Where(p => p.Value is not null).ToDictionary());

    [McpServerTool(Name = "efekt_sec"), Description("Efekt başlatır ('sabit' efekti durdurur). Hız ve skala verilmezse mevcut / varsayılan kalır.")]
    public static Task<string> Efekt(
        [Description("Efekt id'si; liste için 'efektler' aracı")] string id,
        [Description("1–10, 5 normal")] int? hiz = null,
        [Description("Skala adı, ör. 'Neon'; yalnız skalalı efektlerde anlamlı")] string? skala = null) =>
        Gonder(new Dictionary<string, object?> { ["komut"] = "efekt", ["id"] = id, ["hiz"] = hiz, ["skala"] = skala }
            .Where(p => p.Value is not null).ToDictionary());

    static async Task<string> Gonder(object istek)
    {
        try
        {
            await using var boru = new NamedPipeClientStream(".", BoruAdi, PipeDirection.InOut, PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
            await boru.ConnectAsync(2000);
            using var okuyucu = new StreamReader(boru, leaveOpen: true);
            await using var yazici = new StreamWriter(boru, leaveOpen: true) { AutoFlush = true };
            await yazici.WriteLineAsync(JsonSerializer.Serialize(istek));
            return await okuyucu.ReadLineAsync() ?? """{"tamam":false,"hata":"yanıt yok"}""";
        }
        catch (TimeoutException)
        {
            return """{"tamam":false,"hata":"Monster Klavye çalışmıyor (tepside açık olmalı)"}""";
        }
    }
}
