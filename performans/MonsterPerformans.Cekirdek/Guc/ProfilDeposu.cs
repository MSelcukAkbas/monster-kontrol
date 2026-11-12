using System.Text.Json;
using System.Text.Json.Serialization;
using MonsterPerformans.Cekirdek.Fan;

namespace MonsterPerformans.Cekirdek.Guc;

/// <summary>Kilitli 3 profil. Kodda durur, JSON'a yazılmaz: dosya elle bozulsa da değişmezler.</summary>
public static class HazirProfiller
{
    // Frekans % × 4,1 GHz: Sessiz 59 ≈ 2,4 GHz (taban), Dengeli 78 ≈ 3,2 GHz (tam yükte 90°C sınırının altında kalsın), Performans sınırsız.
    // Eğriler yatık (az fark = az fan değişimi); GPU fanı daha sessiz olduğu için CPU'nunkinden önde.
    public static readonly Profil Sessiz = new("Sessiz", GucSeviyesi.Sessiz, WindowsGucPlani.Dengeli, 59, false,
        FanModu.Ozel, HazirEgriler.Sessiz, HazirEgriler.SessizGpu);
    public static readonly Profil Dengeli = new("Dengeli", GucSeviyesi.Eglence, WindowsGucPlani.Dengeli, 78, true,
        FanModu.Ozel, HazirEgriler.Dengeli, HazirEgriler.DengeliGpu);
    public static readonly Profil Performans = new("Performans", GucSeviyesi.Performans, WindowsGucPlani.Dengeli, 100, true,
        FanModu.Ozel, HazirEgriler.Performans, HazirEgriler.PerformansGpu);

    public static IReadOnlyList<Profil> Hepsi { get; } = [Sessiz, Dengeli, Performans];

    public static bool Kilitli(Profil p) => Hepsi.Any(h => ReferenceEquals(h, p));
}

/// <summary>
/// Hazır eğriler (2. ve 3. nokta). Dalgalanmayı azaltmak için t2–t3 arası yatık; t3'ten sonra EC 100°C/%100'e diker.
/// </summary>
public static class HazirEgriler
{
    public static readonly FanEgrisi
        Sessiz = new(50, 20, 85, 45), SessizGpu = new(50, 25, 85, 55),
        Dengeli = new(50, 25, 88, 48), DengeliGpu = new(50, 50, 85, 80),
        Performans = new(50, 40, 88, 75), PerformansGpu = new(50, 50, 85, 90);

    public static IReadOnlyList<(string Ad, FanEgrisi Egri)> Hepsi { get; } =
    [
        ("Sessiz", Sessiz),
        ("Dengeli", Dengeli),
        ("Performans", Performans),
        ("Serin", new(50, 55, 70, 90)),
    ];
}

/// <summary>%AppData%\MonsterPerformans\ayarlar.json: kullanıcı profilleri ve tepsideki tek profil.</summary>
public sealed class PerformansAyarlari
{
    public List<Profil> Profiller { get; set; } = [];

    /// <summary>Tepside en fazla bir kullanıcı profili: tek alan olduğu için kendiliğinden doğru.</summary>
    public string? TepsiProfili { get; set; }

    /// <summary>Acil durum eşiği, 92–95°C.</summary>
    public int AcilEsik { get; set; } = AcilDurum.VarsayilanEsik;

    /// <summary>Açılışta ve uykudan dönüşte son profili yeniden uygula. Kapalı başlar: gözetimsiz yazma kullanıcının seçimi.</summary>
    public bool YenidenUygula { get; set; }

    /// <summary>Son uygulanan profil (güvenli duruma dönünce silinir).</summary>
    public string? SonProfil { get; set; }

    public DogrulamaPolitikasi DogrulamaHatasi { get; set; }

    public bool KayitTut { get; set; } = true;

    /// <summary>Boşsa <see cref="VarsayilanKayitKlasoru"/>.</summary>
    public string? KayitKlasoru { get; set; }

    [JsonIgnore]
    public string Kayit => string.IsNullOrWhiteSpace(KayitKlasoru) ? VarsayilanKayitKlasoru : KayitKlasoru;

    public static string VarsayilanKayitKlasoru { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "MonsterPerformans", "Gunluk"); // gölge kaydedicinin "kayit" klasörü değil: temizlik ham veriyi silmesin

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
            .Select(p => p with { FrekansYuzde = Math.Clamp(p.FrekansYuzde, 1, 100), CpuEgrisi = EnAz(p.CpuEgrisi), GpuEgrisi = EnAz(p.GpuEgrisi) })
            .ToList();
        if (a.Profiller.All(p => p.Ad != a.TepsiProfili)) a.TepsiProfili = null;
        if (a.Bul(a.SonProfil) is null) a.SonProfil = null;
        a.AcilEsik = Math.Clamp(a.AcilEsik, AcilDurum.VarsayilanEsik, AcilDurum.EnYuksekEsik);
        if (!Enum.IsDefined(a.DogrulamaHatasi)) a.DogrulamaHatasi = default;
        return a;
    }

    // Eski profillerde %15 altı fan kalmış olabilir (sınır sonradan yükseldi); boş eğri (Otomatik fan) dokunulmaz
    static FanEgrisi EnAz(FanEgrisi e) => e == default ? e
        : e with { D2 = Math.Max(e.D2, FanEgrisi.EnAz), D3 = Math.Max(e.D3, FanEgrisi.EnAz) };

    /// <summary>Önce geçici dosyaya, sonra yerine taşır: yazarken kapanırsa eski dosya bozulmaz.</summary>
    public void Yaz(string yol)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(yol)!);
        var gecici = yol + ".tmp";
        File.WriteAllText(gecici, JsonSerializer.Serialize(this, Secenek));
        File.Move(gecici, yol, overwrite: true);
    }

    public IEnumerable<Profil> Hepsi => HazirProfiller.Hepsi.Concat(Profiller);

    public Profil? Bul(string? ad) => Hepsi.FirstOrDefault(p => p.Ad == ad);

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
        if (SonProfil == eski.Ad) SonProfil = yeni.Ad;
    }

    public void Sil(Profil p)
    {
        Profiller.Remove(p);
        if (TepsiProfili == p.Ad) TepsiProfili = null;
        if (SonProfil == p.Ad) SonProfil = null;
    }

    public bool AdUygun(string ad) => !string.IsNullOrWhiteSpace(ad) && !AdAyrilmis(ad) && Profiller.All(p => p.Ad != ad);

    static bool AdAyrilmis(string ad) => HazirProfiller.Hepsi.Any(h => string.Equals(h.Ad, ad.Trim(), StringComparison.OrdinalIgnoreCase));
}
