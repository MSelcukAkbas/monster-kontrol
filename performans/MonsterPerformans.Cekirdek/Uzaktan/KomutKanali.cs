using System.IO.Pipes;
using System.Text.Json;

namespace MonsterPerformans.Cekirdek.Uzaktan;

/// <summary>
/// Dış programların (Monster Kontrol arayüzü, MCP sunucusu) performans durumunu okuduğu yerel kanal.
/// Yalnız bu Windows kullanıcısı bağlanabilir. Bağlantı başına tek satır JSON istek → tek satır JSON yanıt.
/// Şimdilik salt okunur: fan / güç yazan komut yok.
/// </summary>
public sealed class KomutKanali : IDisposable
{
    public const string BoruAdi = "MonsterPerformans.Komut";

    readonly CancellationTokenSource _iptal = new();

    /// <param name="calistir">Komutu uygulamanın durumunu güvenle okuyabileceği iş parçacığında çalıştırır.</param>
    public KomutKanali(IReadOnlyDictionary<string, Func<object>> komutlar, Func<Func<string>, string> calistir, string boruAdi = BoruAdi) =>
        _ = Task.Run(() => Dinle(komutlar, calistir, boruAdi));

    async Task Dinle(IReadOnlyDictionary<string, Func<object>> komutlar, Func<Func<string>, string> calistir, string boruAdi)
    {
        while (!_iptal.IsCancellationRequested)
        {
            try
            {
                await using var boru = new NamedPipeServerStream(boruAdi, PipeDirection.InOut, 1,
                    PipeTransmissionMode.Byte, PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
                await boru.WaitForConnectionAsync(_iptal.Token);
                using var okuyucu = new StreamReader(boru, leaveOpen: true);
                await using var yazici = new StreamWriter(boru, leaveOpen: true) { AutoFlush = true };
                var istek = await okuyucu.ReadLineAsync(_iptal.Token).AsTask().WaitAsync(TimeSpan.FromSeconds(5), _iptal.Token);
                await yazici.WriteLineAsync(istek is null ? Hata("boş istek") : calistir(() => Isle(istek, komutlar)));
            }
            catch (OperationCanceledException) when (_iptal.IsCancellationRequested) { return; }
            catch (Exception ex) when (ex is IOException or TimeoutException) { } // istemci koptu / yazmadı: sıradakini bekle
        }
    }

    /// <summary>Tek isteği yanıtlar. Girdi güvenilmez: bilinmeyen komut reddedilir.</summary>
    public static string Isle(string istek, IReadOnlyDictionary<string, Func<object>> komutlar)
    {
        try
        {
            using var belge = JsonDocument.Parse(istek);
            var k = belge.RootElement;
            if (k.ValueKind != JsonValueKind.Object || !k.TryGetProperty("komut", out var komut) || komut.ValueKind != JsonValueKind.String)
                return Hata("'komut' alanı gerekli");
            return komutlar.TryGetValue(komut.GetString()!, out var f)
                ? JsonSerializer.Serialize(f())
                : Hata($"bilinmeyen komut: {komut.GetString()}");
        }
        catch (JsonException) { return Hata("geçersiz JSON"); }
    }

    static string Hata(string mesaj) => JsonSerializer.Serialize(new { tamam = false, hata = mesaj });

    public void Dispose() => _iptal.Cancel();
}
