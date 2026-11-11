using System.Text.Json;
using System.Text.Json.Serialization;
using MonsterPerformans.Cekirdek.Fan;

namespace MonsterPerformans.Cekirdek.Guc;

/// <summary>Kilitli 3 profil. Kodda durur, JSON'a yazılmaz: dosya elle bozulsa da değişmezler.</summary>
public static class HazirProfiller
{
    public static readonly Profil Sessiz = new("Sessiz", GucSeviyesi.Sessiz, WindowsGucPlani.Dengeli, 70, false, FanModu.Otomatik);
    public static readonly Profil Dengeli = new("Dengeli", GucSeviyesi.Eglence, WindowsGucPlani.Dengeli, 100, true, FanModu.Otomatik);
    public static readonly Profil Performans = new("Performans", GucSeviyesi.Performans, WindowsGucPlani.Dengeli, 100, true, FanModu.Maksimum);

    public static IReadOnlyList<Profil> Hepsi { get; } = [Sessiz, Dengeli, Performans];

    public static bool Kilitli(Profil p) => Hepsi.Any(h => ReferenceEquals(h, p));
}

/// <summary>Fan sayfasındaki hazır eğriler (2. ve 3. nokta). Taban %35 EC'de sabit: Sessiz yalnız geç ve yavaş yükselir.</summary>
public static class HazirEgriler
{
    public static IReadOnlyList<(string Ad, FanEgrisi Egri)> Hepsi { get; } =
    [
        ("Sessiz", new(65, 40, 85, 70)),
        ("Dengeli", new(55, 50, 80, 80)),
        ("Serin", new(50, 55, 70, 90)),
    ];

    public static FanEgrisi Dengeli => Hepsi[1].Egri;
}

/// <summary>%AppData%\MonsterPerformans\ayarlar.json: kullanıcı profilleri ve tepsideki tek profil.</summary>
public sealed class PerformansAyarlari
{
    public List<Profil> Profiller { get; set; } = [];

    /// <summary>Tepside en fazla bir kullanıcı profili: tek alan olduğu için kendiliğinden doğru.</summary>
    public string? TepsiProfili { get; set; }

    public static string VarsayilanYol { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "MonsterPerformans", "ayarlar.json");

    static readonly JsonSerializerOptions Secenek = new() { WriteIndented = true, Converters = { new JsonStringEnumConverter() } };

    /// <summary>Dosya yoksa ya da bozuksa boş ayarlar. Elle düzenlenmiş değerler sınırlanır.</summary>
    public static PerformansAyarlari Oku(string yol)
    {
        PerformansAyarlari a;
        try { a = JsonSerializer.Deserialize<PerformansAyarlari>(File.ReadAllText(yol), Secenek) ?? new(); }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or JsonException or NotSupportedException) { return new(); }

        a.Profiller = (a.Profiller ?? [])
            .Where(p => p is not null && !string.IsNullOrWhiteSpace(p.Ad) && !AdAyrilmis(p.Ad))
            .DistinctBy(p => p.Ad)
            .Select(p => p with { FrekansYuzde = Math.Clamp(p.FrekansYuzde, 1, 100) })
            .ToList();
        if (a.Profiller.All(p => p.Ad != a.TepsiProfili)) a.TepsiProfili = null;
        return a;
    }

    /// <summary>Önce geçici dosyaya, sonra yerine taşır: yazarken kapanırsa eski dosya bozulmaz.</summary>
    public void Yaz(string yol)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(yol)!);
        var gecici = yol + ".tmp";
        File.WriteAllText(gecici, JsonSerializer.Serialize(this, Secenek));
        File.Move(gecici, yol, overwrite: true);
    }

    public IEnumerable<Profil> Hepsi => HazirProfiller.Hepsi.Concat(Profiller);

    /// <summary>Kopya listeye eklenir. Eğrisi boşsa (hazır profiller) Dengeli eğriyle başlar.</summary>
    public Profil Kopyala(Profil kaynak)
    {
        var ad = $"{kaynak.Ad} kopyası";
        for (var i = 2; !AdUygun(ad); i++) ad = $"{kaynak.Ad} kopyası {i}";
        var kopya = kaynak with
        {
            Ad = ad,
            CpuEgrisi = kaynak.CpuEgrisi == default ? HazirEgriler.Dengeli : kaynak.CpuEgrisi,
            GpuEgrisi = kaynak.GpuEgrisi == default ? HazirEgriler.Dengeli : kaynak.GpuEgrisi,
        };
        Profiller.Add(kopya);
        return kopya;
    }

    /// <summary>Kullanıcı profilini yenisiyle değiştirir; ad değiştiyse tepsi bağlantısı da taşınır.</summary>
    public void Degistir(Profil eski, Profil yeni)
    {
        var i = Profiller.IndexOf(eski);
        if (i < 0) throw new ArgumentException("Profil listede yok", nameof(eski));
        if (eski.Ad != yeni.Ad && !AdUygun(yeni.Ad)) throw new ArgumentException("Bu ad kullanılıyor ya da boş", nameof(yeni));
        Profiller[i] = yeni;
        if (TepsiProfili == eski.Ad) TepsiProfili = yeni.Ad;
    }

    public void Sil(Profil p)
    {
        Profiller.Remove(p);
        if (TepsiProfili == p.Ad) TepsiProfili = null;
    }

    public bool AdUygun(string ad) => !string.IsNullOrWhiteSpace(ad) && !AdAyrilmis(ad) && Profiller.All(p => p.Ad != ad);

    static bool AdAyrilmis(string ad) => HazirProfiller.Hepsi.Any(h => string.Equals(h.Ad, ad.Trim(), StringComparison.OrdinalIgnoreCase));
}
