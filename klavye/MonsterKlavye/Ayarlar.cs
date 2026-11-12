using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using MonsterKlavye.Klavye;

namespace MonsterKlavye;

/// <summary>%AppData%\MonsterKlavye\ayarlar.json içeriği. Eksik alanlar varsayılanı alır.
/// "Windows ile başlat" burada değil: tek doğru kaynak kayıt defteri (<see cref="WindowsIleBaslat"/>).</summary>
public sealed record Ayarlar(
    string Renk = "#FF6A00",
    int Parlaklik = 80,
    bool Acik = true,
    string Efekt = "sabit",
    int Hiz = 5,
    string? Skala = null)
{
    public static string VarsayilanYol { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "MonsterKlavye", "ayarlar.json");

    static readonly JsonSerializerOptions Secenek = new() { WriteIndented = true };

    /// <summary>Dosya yoksa ya da bozuksa varsayılanlar (ışık yine açılır, uygulama düşmez).</summary>
    public static Ayarlar Oku(string yol)
    {
        try { return JsonSerializer.Deserialize<Ayarlar>(File.ReadAllText(yol)) ?? new(); }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or JsonException) { return new(); }
    }

    /// <summary>Önce geçici dosyaya yazar, sonra yerine taşır: yazarken kapanırsa eski dosya bozulmaz.</summary>
    public void Yaz(string yol)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(yol)!);
        var gecici = yol + ".tmp";
        File.WriteAllText(gecici, JsonSerializer.Serialize(this, Secenek));
        File.Move(gecici, yol, overwrite: true);
    }

    public static Ayarlar Al(EfektMotoru m)
    {
        var (t, a) = (m.Temel, m.Ayarlar);
        return new(t.Renk.Hex, t.ParlaklikYuzde, t.Acik, m.Efekt.Id, a.Hiz, a.Skala.Ad);
    }

    // Dosya kullanıcı elinden geçebilir: değerler sınırlanır, bilinmeyen efekt/skala varsayılana düşer.
    [JsonIgnore] public KlavyeDurumu Durum => new(Rgb.HexCoz(Renk) ?? new Rgb(0xFF, 0x6A, 0x00), Math.Clamp(Parlaklik, 0, 100), Acik);

    // Hepsi'deki örneği döndürür: motor efektleri ReferenceEquals ile karşılaştırır.
    [JsonIgnore] public Efekt EtkinEfekt => Efektler.Hepsi.FirstOrDefault(e => e.Id == Efekt) ?? Efektler.Sabit;

    [JsonIgnore] public EfektAyarlari EfektAyarlari => new(Math.Clamp(Hiz, 1, 10),
        Efektler.Skalalar.FirstOrDefault(s => s.Ad == Skala) ?? EtkinEfekt.VarsayilanSkala ?? Efektler.TamCark);
}
