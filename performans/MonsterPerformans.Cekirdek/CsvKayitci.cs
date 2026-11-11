using System.Globalization;
using System.Text;
using MonsterPerformans.Cekirdek.Sensorler;

namespace MonsterPerformans.Cekirdek;

/// <summary>Uygulamanın o saniyedeki durumu: son profil, fan modu, acil kademe, ölçüm sürüyor mu.</summary>
public readonly record struct UygulamaDurumu(string? Profil, string? Fan, string? Acil, bool Olcum);

/// <summary>
/// Ham kayıt: günde bir CSV (klasör/yyyy-MM-dd.csv), ekleyerek yazar, her satırda diske boşaltır
/// (çökmede en çok bir satır kaybolur). Ondalık ayırıcı nokta (kültürden bağımsız).
/// Sütunlar değişince (yeni sürüm) aynı günün eski dosyasına eklenmez: yyyy-MM-dd_HHmmss.csv açılır.
/// </summary>
public sealed class CsvKayitci(string klasor, bool ham = true) : IDisposable
{
    static readonly CultureInfo I = CultureInfo.InvariantCulture;

    public static readonly string Baslik =
        "zaman,cpu_C,kart_C,cpu_duty,cpu_rpm,gpu_duty,gpu_rpm,profil_b30,yuk,perf,taban_mhz,gercek_mhz,sinir_bayrak,sinir_yuzde,kuyruk," +
        string.Join(',', PdhSayaclari.Sayaclar[6..].Select(s => s.Sutun)) +
        ",gpu_C,gpu_kullanim,gpu_W,gpu_mhz,gpu_bellek_yuk,gpu_bellek_mhz,gpu_p,gpu_kisit,gpu_vram_mb" +
        ",on_plan,bildirim,priz,pil,bosta_sn,plan,frekans_siniri,turbo,epp" +
        ",uyg_profil,uyg_fan,uyg_acil,uyg_olcum,ham";

    public static string BaslikHamsiz => Baslik[..Baslik.LastIndexOf(',')];

    StreamWriter? _yazici;
    DateOnly _gun;

    public static string Satir(Olcum o, UygulamaDurumu? u = null, bool ham = true)
    {
        string S(double? d) => d?.ToString("0.#", I) ?? "";
        string M(string? s) => s?.Replace(',', ';') ?? ""; // metindeki virgül sütunları kaydırmasın
        string B(bool? b) => b is { } x ? (x ? "1" : "0") : "";
        double? P(int k) => k < o.Pdh.Length ? o.Pdh[k] : null;
        var d = o.Dchu;
        var g = o.Gpu;
        var b = o.Baglam;
        var ek = string.Join(',', Enumerable.Range(6, PdhSayaclari.Sayaclar.Length - 6).Select(k => S(P(k))));
        return string.Join(',',
            o.Zaman.ToString("HH:mm:ss", I), d.CpuC, d.KartC, d.CpuDuty, d.CpuRpm, d.GpuDuty, d.GpuRpm, d.ProfilBayti,
            S(P(0)), S(P(1)), S(P(2)), S(o.GercekMHz), S(P(3)), S(P(4)), S(P(5)), ek,
            S(g?.C), S(g?.Kullanim), S(g?.Watt), S(g?.MHz), S(g?.BellekKullanim), S(g?.BellekMHz), S(g?.PDurumu),
            g?.Kisit is { } kisit ? $"0x{kisit:X}" : "", S(g?.VramMB),
            M(b?.OnPlan), S(b?.Bildirim), B(b?.Priz), S(b?.Pil), S(b?.BostaSn), M(b?.Plan), S(b?.FrekansSiniri), S(b?.Turbo), S(b?.Epp),
            M(u?.Profil), M(u?.Fan), M(u?.Acil), B(u?.Olcum))
            + (ham ? "," + Convert.ToHexString(o.HamTampon) : "");
    }

    /// <summary>
    /// Adı yyyy-MM-dd ile başlayan ve <paramref name="gun"/> günden eski CSV'leri önce klasör/ozet altına dakikalık
    /// özetler, sonra siler (özet kalıcı). Özet yazılamazsa silmez. Başka dosyaya dokunmaz.
    /// </summary>
    public static int Temizle(string klasor, int gun, DateOnly bugun)
    {
        if (!Directory.Exists(klasor)) return 0;
        var sinir = bugun.AddDays(-gun);
        var n = 0;
        foreach (var yol in Directory.EnumerateFiles(klasor, "*.csv"))
        {
            var ad = Path.GetFileNameWithoutExtension(yol);
            if (ad.Length < 10 || (ad.Length > 10 && ad[10] != '_')
                || !DateOnly.TryParseExact(ad[..10], "yyyy-MM-dd", I, DateTimeStyles.None, out var t) || t >= sinir)
                continue;
            try
            {
                Ozetle(yol, Path.Combine(klasor, "ozet", ad + ".csv"));
                File.Delete(yol);
                n++;
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException) { } // açık dosya: bir sonraki temizlikte
        }
        return n;
    }

    /// <summary>Dakika başına bir satır: sayısal sütunların ortalaması, diğerlerinin en sık değeri; ham atlanır.</summary>
    public static void Ozetle(string kaynak, string hedef)
    {
        var satirlar = Satirlar(kaynak);
        if (satirlar.Count == 0) return;
        var baslik = satirlar[0].Split(',');
        var sutunlar = Enumerable.Range(1, baslik.Length - 1).Where(i => baslik[i] != "ham").ToArray();
        Directory.CreateDirectory(Path.GetDirectoryName(hedef)!);
        using var w = new StreamWriter(hedef, false, new UTF8Encoding(false));
        w.WriteLine("dakika,ornek," + string.Join(',', sutunlar.Select(i => baslik[i])));
        foreach (var dakika in satirlar.Skip(1).Select(s => s.Split(','))
                     .Where(a => a.Length == baslik.Length && a[0].Length >= 5).GroupBy(a => a[0][..5]))
        {
            var g = dakika.ToList();
            w.WriteLine($"{dakika.Key},{g.Count}," + string.Join(',', sutunlar.Select(i => Ozet(g.Select(a => a[i])))));
        }
    }

    static string Ozet(IEnumerable<string> degerler)
    {
        var dolu = degerler.Where(d => d != "").ToList();
        if (dolu.Count == 0) return "";
        var sayilar = dolu.Select(d => double.TryParse(d, NumberStyles.Float, I, out var x) ? x : (double?)null).ToList();
        return sayilar.All(x => x is not null)
            ? sayilar.Average()!.Value.ToString("0.##", I)
            : dolu.GroupBy(d => d).MaxBy(x => x.Count())!.Key;
    }

    /// <summary>Kayıtçı aynı dosyayı yazarken de okunabilsin.</summary>
    static List<string> Satirlar(string yol, int enCok = int.MaxValue)
    {
        using var r = new StreamReader(new FileStream(yol, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete));
        var l = new List<string>();
        while (l.Count < enCok && r.ReadLine() is { } s) l.Add(s);
        return l;
    }

    public void Yaz(Olcum o, UygulamaDurumu? u = null)
    {
        var gun = DateOnly.FromDateTime(o.Zaman);
        if (_yazici is null || gun != _gun)
        {
            _yazici?.Dispose();
            Directory.CreateDirectory(klasor);
            var baslik = ham ? Baslik : BaslikHamsiz;
            var gunluk = Path.Combine(klasor, $"{gun:yyyy-MM-dd}.csv");
            var yol = Directory.EnumerateFiles(klasor, $"{gun:yyyy-MM-dd}*.csv").Order()
                          .FirstOrDefault(y => Satirlar(y, 1) is [var ilk] && ilk == baslik)
                      ?? (File.Exists(gunluk) ? Path.Combine(klasor, $"{gun:yyyy-MM-dd}_{o.Zaman:HHmmss}.csv") : gunluk);
            var yeni = !File.Exists(yol);
            _yazici = new StreamWriter(yol, append: true, new UTF8Encoding(false)) { AutoFlush = true };
            if (yeni) _yazici.WriteLine(baslik);
            _gun = gun;
        }
        _yazici.WriteLine(Satir(o, u, ham));
    }

    public void Dispose() => _yazici?.Dispose();
}
