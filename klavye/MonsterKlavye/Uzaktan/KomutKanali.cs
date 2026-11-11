using System.IO;
using System.IO.Pipes;
using System.Text.Json;
using System.Windows.Threading;
using MonsterKlavye.Klavye;

namespace MonsterKlavye.Uzaktan;

/// <summary>
/// Dış programların (MCP sunucusu) klavyeyi sürdüğü yerel kanal. Yalnız bu Windows kullanıcısı bağlanabilir.
/// Bağlantı başına tek satır JSON istek → tek satır JSON yanıt. Yalnız klavye komutları vardır: fan / güç bu kanaldan geçemez.
/// </summary>
public sealed class KomutKanali : IDisposable
{
    public const string BoruAdi = "MonsterKlavye.Komut";

    readonly CancellationTokenSource _iptal = new();

    public KomutKanali(Dispatcher arayuz) => _ = Task.Run(() => Dinle(arayuz));

    async Task Dinle(Dispatcher arayuz)
    {
        while (!_iptal.IsCancellationRequested)
        {
            try
            {
                await using var boru = new NamedPipeServerStream(BoruAdi, PipeDirection.InOut, 1,
                    PipeTransmissionMode.Byte, PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
                await boru.WaitForConnectionAsync(_iptal.Token);
                using var okuyucu = new StreamReader(boru, leaveOpen: true);
                await using var yazici = new StreamWriter(boru, leaveOpen: true) { AutoFlush = true };
                var istek = await okuyucu.ReadLineAsync(_iptal.Token).AsTask().WaitAsync(TimeSpan.FromSeconds(5), _iptal.Token);
                // Motor olayları arayüzü günceller: komut UI iş parçacığında işlenir.
                var yanit = istek is null ? Hata("boş istek") : arayuz.Invoke(() => Isle(istek, App.Motor));
                await yazici.WriteLineAsync(yanit);
            }
            catch (OperationCanceledException) when (_iptal.IsCancellationRequested) { return; }
            catch (Exception ex) when (ex is IOException or TimeoutException) { } // istemci koptu / yazmadı: sıradakini bekle
        }
    }

    /// <summary>Tek isteği uygular. Girdi güvenilmez: her alan doğrulanır, bilinmeyen komut reddedilir.</summary>
    internal static string Isle(string istek, EfektMotoru motor)
    {
        JsonDocument belge;
        try { belge = JsonDocument.Parse(istek); }
        catch (JsonException) { return Hata("geçersiz JSON"); }
        using var belgeyiBirak = belge;
        var k = belge.RootElement;
        if (k.ValueKind != JsonValueKind.Object || !k.TryGetProperty("komut", out var komut) || komut.ValueKind != JsonValueKind.String)
            return Hata("'komut' alanı gerekli");

        try
        {
            switch (komut.GetString())
            {
                case "durum":
                    break;
                case "efektler":
                    return JsonSerializer.Serialize(new
                    {
                        efektler = Efektler.Hepsi.Select(e => new { id = e.Id, ad = e.Ad, aciklama = e.Aciklama, skalali = e.SkalaVar }),
                        skalalar = Efektler.Skalalar.Select(s => s.Ad),
                    });
                case "renk":
                    var renk = Rgb.HexCoz(Metin(k, "deger")) ?? throw new ArgumentException("renk #RRGGBB olmalı");
                    motor.TemelAyarla(motor.Temel with { Renk = renk, Acik = true });
                    if (motor.Efekt.SkalaVar) motor.EfektAyarla(Efektler.Sabit); // arayüzle aynı: skalalı efekt seçilen rengi kullanmaz
                    break;
                case "parlaklik":
                    var p = Sayi(k, "deger", 0, 100);
                    motor.TemelAyarla(motor.Temel with { ParlaklikYuzde = p, Acik = motor.Temel.Acik || p > 0 });
                    break;
                case "isik":
                    if (!k.TryGetProperty("acik", out var acik) || acik.ValueKind is not (JsonValueKind.True or JsonValueKind.False))
                        throw new ArgumentException("'acik' true/false olmalı");
                    motor.TemelAyarla(motor.Temel with { Acik = acik.GetBoolean() });
                    break;
                case "efekt":
                    var id = Metin(k, "id");
                    var efekt = Efektler.Hepsi.FirstOrDefault(e => e.Id == id) ?? throw new ArgumentException($"bilinmeyen efekt: {id}");
                    var ayni = ReferenceEquals(efekt, motor.Efekt);
                    var hiz = k.TryGetProperty("hiz", out _) ? Sayi(k, "hiz", 1, 10) : ayni ? motor.Ayarlar.Hiz : 5;
                    var skala = k.TryGetProperty("skala", out _)
                        ? Efektler.Skalalar.FirstOrDefault(s => s.Ad == Metin(k, "skala")) ?? throw new ArgumentException("bilinmeyen skala")
                        : ayni ? motor.Ayarlar.Skala : efekt.VarsayilanSkala ?? Efektler.TamCark;
                    if (!motor.Temel.Acik) motor.TemelAyarla(motor.Temel with { Acik = true }); // efekt uygulamak ışığı açar
                    motor.EfektAyarla(efekt, new EfektAyarlari(hiz, skala));
                    break;
                case "uyari":
                    var uyariRengi = k.TryGetProperty("renk", out _)
                        ? Rgb.HexCoz(Metin(k, "renk")) ?? throw new ArgumentException("renk #RRGGBB olmalı")
                        : new Rgb(255, 0, 0);
                    motor.Uyar(uyariRengi, k.TryGetProperty("kez", out _) ? Sayi(k, "kez", 1, 20) : 3);
                    break;
                default:
                    return Hata($"bilinmeyen komut: {komut.GetString()}");
            }
        }
        catch (ArgumentException ex) { return Hata(ex.Message); }

        var t = motor.Temel;
        return JsonSerializer.Serialize(new
        {
            tamam = true,
            renk = t.Renk.Hex,
            parlaklik = t.ParlaklikYuzde,
            acik = t.Acik,
            efekt = motor.Efekt.Id,
            hiz = motor.Ayarlar.Hiz,
            skala = motor.Efekt.SkalaVar ? motor.Ayarlar.Skala.Ad : null,
        });
    }

    static string Metin(JsonElement k, string ad) =>
        k.TryGetProperty(ad, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString()! : throw new ArgumentException($"'{ad}' metin olmalı");

    static int Sayi(JsonElement k, string ad, int en, int cok) =>
        k.TryGetProperty(ad, out var v) && v.ValueKind == JsonValueKind.Number && v.TryGetInt32(out var n) && n >= en && n <= cok
            ? n : throw new ArgumentException($"'{ad}' {en}–{cok} arası tam sayı olmalı");

    static string Hata(string mesaj) => JsonSerializer.Serialize(new { tamam = false, hata = mesaj });

    public void Dispose() => _iptal.Cancel();
}
