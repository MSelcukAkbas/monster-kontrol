using System.Globalization;
using MonsterPerformans.Cekirdek.Sensorler;

namespace MonsterPerformans.Cekirdek;

/// <summary>
/// Açılışta son N saniyeyi günlük CSV'lerden geri yükler: arayüz geçmişi uygulama yeniden başlayınca boş kalmasın.
/// Sütunlar başlık adıyla bulunur (eski sürüm dosyaları da okunur).
/// </summary>
public static class GecmisYukleyici
{
    static readonly CultureInfo I = CultureInfo.InvariantCulture;

    /// <summary>
    /// Saniye başına bir ölçüm, ilk kayıttan <paramref name="simdi"/>'ye kadar. Kaydın olmadığı saniyeler (uygulama kapalıydı)
    /// geçersiz ölçümle doldurulur: grafik zamana göre kalsın, boşluk çizilmesin.
    /// </summary>
    public static List<Olcum> Olcumler(string klasor, DateTime simdi, int saniye)
    {
        var bas = simdi.AddSeconds(-saniye);
        var bulunan = new SortedDictionary<DateTime, Olcum>();
        foreach (var (zaman, a, s) in Satirlar(klasor, bas, simdi))
        {
            if (bulunan.ContainsKey(zaman)) continue;
            int Tam(string ad) => s.TryGetValue(ad, out var i) && i < a.Length && int.TryParse(a[i], NumberStyles.Integer, I, out var x) ? x : 0;
            double? Ond(string ad) => s.TryGetValue(ad, out var i) && i < a.Length && double.TryParse(a[i], NumberStyles.Float, I, out var x) ? x : null;
            var pdh = new double?[PdhSayaclari.Sayaclar.Length];
            (pdh[0], pdh[1], pdh[2]) = (Ond("yuk"), Ond("perf"), Ond("taban_mhz"));
            var gpuC = Ond("gpu_C");
            bulunan[zaman] = new Olcum(zaman,
                new DchuDurumu(Tam("cpu_C"), Tam("kart_C"), Tam("cpu_duty"), Tam("gpu_duty"), Tam("cpu_rpm"), Tam("gpu_rpm"), 0),
                pdh, gpuC is { } g ? new GpuDurumu((int)g, null, null, null) : null, []);
        }
        var l = new List<Olcum>();
        if (bulunan.Count == 0) return l;
        for (var t = bulunan.Keys.First(); t <= simdi; t = t.AddSeconds(1))
            l.Add(bulunan.TryGetValue(t, out var o) ? o : new Olcum(t, default, new double?[PdhSayaclari.Sayaclar.Length], null, []));
        return l;
    }

    /// <summary>Saniye başına uygulama payları (surecler/ klasörü). Kayıt olmayan saniye atlanır.</summary>
    public static List<IReadOnlyList<SurecPayi>> Surecler(string klasor, DateTime simdi, int saniye) =>
        Satirlar(Path.Combine(klasor, "surecler"), simdi.AddSeconds(-saniye), simdi)
            .GroupBy(r => r.Zaman)
            .OrderBy(g => g.Key)
            .Select(g => (IReadOnlyList<SurecPayi>)g.Select(r =>
            {
                string D(string ad) => r.Sutun.TryGetValue(ad, out var i) && i < r.Alan.Length ? r.Alan[i] : "";
                double S(string ad) => double.TryParse(D(ad), NumberStyles.Float, I, out var x) ? x : 0;
                return new SurecPayi(D("uygulama"), (int)S("surec"), S("cpu_yuzde"), S("gpu_yuzde"),
                    double.TryParse(D("tahmini_cpu_W"), NumberStyles.Float, I, out var w) ? w : null);
            }).ToList())
            .ToList();

    /// <summary>[bas, son] aralığındaki satırlar; dosya adı yyyy-MM-dd*.csv (gün), ilk sütun HH:mm:ss.</summary>
    static IEnumerable<(DateTime Zaman, string[] Alan, Dictionary<string, int> Sutun)> Satirlar(string klasor, DateTime bas, DateTime son)
    {
        if (!Directory.Exists(klasor)) yield break;
        for (var gun = DateOnly.FromDateTime(bas); gun <= DateOnly.FromDateTime(son); gun = gun.AddDays(1))
        {
            foreach (var yol in Directory.EnumerateFiles(klasor, $"{gun:yyyy-MM-dd}*.csv").Order())
            {
                List<string> satirlar;
                try { satirlar = Oku(yol); }
                catch (IOException) { continue; }
                if (satirlar.Count == 0) continue;
                var baslik = satirlar[0].Split(',');
                var sutun = Enumerable.Range(0, baslik.Length).ToDictionary(i => baslik[i], i => i);
                foreach (var s in satirlar.Skip(1))
                {
                    var a = s.Split(',');
                    if (!TimeOnly.TryParseExact(a[0], "HH:mm:ss", I, DateTimeStyles.None, out var saat)) continue;
                    var zaman = gun.ToDateTime(saat);
                    if (zaman >= bas && zaman <= son) yield return (zaman, a, sutun);
                }
            }
        }
    }

    /// <summary>Kayıtçı yazarken de okunur. Büyük dosyada yalnız sondan ~ihtiyaç kadarı: günlük CSV ham tamponla 80 MB'a varabilir.</summary>
    static List<string> Oku(string yol)
    {
        using var f = new FileStream(yol, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        using var r = new StreamReader(f);
        var l = new List<string>();
        if (r.ReadLine() is not { } baslik) return l;
        l.Add(baslik);
        const long Son = 8 * 1024 * 1024; // ana CSV satırı ~1,2 KB → 30 dk ≈ 2,2 MB; süreç CSV'si 30 dk ≈ 1 MB
        if (f.Length - f.Position > Son)
        {
            f.Seek(-Son, SeekOrigin.End);
            r.DiscardBufferedData();
            r.ReadLine(); // yarım satır
        }
        while (r.ReadLine() is { } s) l.Add(s);
        return l;
    }
}
