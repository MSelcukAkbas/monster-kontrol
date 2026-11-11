using System.ComponentModel;
using System.IO.Pipes;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;

// Claude gibi MCP istemcileri için performans araçları. Donanıma dokunmaz: çalışan Monster Performans'a
// yerel kanaldan sorar. Şimdilik salt okunur; fan / güç yazan araç yok.
var builder = Host.CreateApplicationBuilder(args);
builder.Logging.AddConsole(o => o.LogToStandardErrorThreshold = LogLevel.Trace); // stdout MCP'ye ait
builder.Services.AddMcpServer().WithStdioServerTransport().WithTools<PerformansAraclari>();
await builder.Build().RunAsync();

[McpServerToolType]
public class PerformansAraclari
{
    const string BoruAdi = "MonsterPerformans.Komut"; // Cekirdek KomutKanali.BoruAdi ile aynı

    [McpServerTool(Name = "durum", ReadOnly = true), Description(
        "Son 1 sn'lik ölçüm: CPU/GPU sıcaklığı (°C), fan yüzdesi (duty) ve devri (rpm), GPU watt, ortalama CPU MHz, " +
        "Windows güç planı, etkin profil, fan modu, acil durum kademesi ve son uygulama özeti. GPU sıcaklığı uykudaysa boş olabilir.")]
    public static Task<string> Durum() => Gonder(new { komut = "durum" });

    [McpServerTool(Name = "profiller", ReadOnly = true), Description("Tüm profiller (ad, güç seviyesi, fan modu, hazır mı) ve etkin profil.")]
    public static Task<string> Profiller() => Gonder(new { komut = "profiller" });

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
            return """{"tamam":false,"hata":"Monster Performans çalışmıyor (tepside açık olmalı)"}""";
        }
    }
}
