using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;

namespace MonsterPerformans.Cekirdek;

/// <summary>Bir uygulamanın (aynı exe adlı süreçlerin toplamı) o saniyedeki payı.</summary>
public readonly record struct SurecPayi(string Ad, int Adet, double Cpu, double Gpu, double? Watt);

/// <summary>Bir pencere boyunca: ortalama CPU/GPU %, tahmini enerji (Wh, saniyelik örnekten), görüldüğü saniye.</summary>
public readonly record struct SurecOzeti(string Ad, double Cpu, double Gpu, double? Wh, int Saniye);

/// <summary>
/// Saniyede bir, CPU'su ya da GPU'su %1 ve üstü olan her uygulamayı ölçer; Klasor doluysa klasor/surecler/yyyy-MM-dd.csv'ye yazar.
/// Kaynak PDH (İngilizce adla, yönetici gerekmez, dGPU'yu uyandırmaz): Process ve GPU Engine sayaçları.
/// CPU % tüm işlemciye göre (Görev Yöneticisi gibi); GPU % uygulamanın en yoğun motoru; W = paket gücünün CPU payı (tahmin).
/// </summary>
public sealed class SurecKayitci : IDisposable
{
    public const double Esik = 1;
    public const string Baslik = "zaman,uygulama,surec,cpu_yuzde,gpu_yuzde,tahmini_cpu_W";
    static readonly CultureInfo I = CultureInfo.InvariantCulture;

    readonly IntPtr _sorgu, _cpu, _pid, _gpu;
    StreamWriter? _yazici;
    DateOnly _gun;
    string? _acik;

    /// <summary>Kayıt klasörü (alt klasör surecler eklenir); null: yalnız ölçer, yazmaz.</summary>
    public string? Klasor { get; set; }

    public SurecKayitci()
    {
        if (PdhOpenQueryW(null, IntPtr.Zero, out _sorgu) != 0) throw new InvalidOperationException("PdhOpenQuery başarısız");
        PdhAddEnglishCounterW(_sorgu, @"\Process(*)\% Processor Time", IntPtr.Zero, out _cpu);
        PdhAddEnglishCounterW(_sorgu, @"\Process(*)\ID Process", IntPtr.Zero, out _pid);
        PdhAddEnglishCounterW(_sorgu, @"\GPU Engine(*)\Utilization Percentage", IntPtr.Zero, out _gpu);
        PdhCollectQueryData(_sorgu); // oran sayaçları iki örnek ister
    }

    /// <summary>Örnek alır ve eşiği geçenleri yazar. <paramref name="cpuMW"/> = RAPL paket gücü (yoksa W boş).</summary>
    public IReadOnlyList<SurecPayi> Yaz(DateTime zaman, double? cpuMW)
    {
        PdhCollectQueryData(_sorgu);
        var paylar = Grupla(Dizi(_cpu), Dizi(_pid), Dizi(_gpu), Environment.ProcessorCount, cpuMW);
        var gun = DateOnly.FromDateTime(zaman);
        var klasor = Klasor is { } k ? Path.Combine(k, "surecler") : null;
        if (klasor != _acik) { _yazici?.Dispose(); _yazici = null; _acik = klasor; }
        if (klasor is null) return paylar;
        if (_yazici is null || gun != _gun)
        {
            _yazici?.Dispose();
            Directory.CreateDirectory(klasor);
            var yol = Path.Combine(klasor, $"{gun:yyyy-MM-dd}.csv");
            var yeni = !File.Exists(yol);
            _yazici = new StreamWriter(yol, append: true, new UTF8Encoding(false)) { AutoFlush = false };
            if (yeni) _yazici.WriteLine(Baslik);
            _gun = gun;
        }
        var t = zaman.ToString("HH:mm:ss", I);
        foreach (var p in paylar)
            _yazici.WriteLine($"{t},{p.Ad.Replace(',', ';')},{p.Adet},{p.Cpu.ToString("0.#", I)},{p.Gpu.ToString("0.#", I)},{p.Watt?.ToString("0.##", I)}");
        _yazici.Flush();
        return paylar;
    }

    /// <summary>
    /// Saf hesap (test edilir). cpu/pid örnek adları "chrome", "chrome#3" biçiminde; GPU örneği "pid_1234_..._engtype_3D".
    /// </summary>
    public static List<SurecPayi> Grupla(IEnumerable<(string Ornek, double Deger)> cpu, IEnumerable<(string Ornek, double Deger)> pid,
        IEnumerable<(string Ornek, double Deger)> gpu, int cekirdek, double? cpuMW)
    {
        static string Ad(string ornek) { var i = ornek.IndexOf('#'); return i < 0 ? ornek : ornek[..i]; }
        static bool Gercek(string o) => o is not ("_Total" or "Idle");

        var pidAd = pid.Where(p => Gercek(p.Ornek)).GroupBy(p => (int)p.Deger).ToDictionary(g => g.Key, g => Ad(g.First().Ornek));
        var cpuAd = cpu.Where(c => Gercek(c.Ornek)).GroupBy(c => Ad(c.Ornek))
            .ToDictionary(g => g.Key, g => (Adet: g.Count(), Cpu: g.Sum(c => c.Deger) / cekirdek));
        var toplam = cpuAd.Values.Sum(v => v.Cpu);

        // Uygulamanın her motor türü (3D, Copy, VideoDecode…) toplanır, en yoğun tür GPU %'si olur (Görev Yöneticisi gibi)
        var gpuAd = new Dictionary<string, double>();
        foreach (var g in gpu.Select(g => (Ornek: g.Ornek.Split('_'), g.Deger))
                     .Where(g => g.Ornek.Length > 1 && g.Ornek[0] == "pid" && int.TryParse(g.Ornek[1], out _))
                     .Select(g => (Ad: pidAd.GetValueOrDefault(int.Parse(g.Ornek[1]), $"pid {g.Ornek[1]}"), Tur: g.Ornek[^1], g.Deger))
                     .GroupBy(g => (g.Ad, g.Tur))
                     .Select(g => (g.Key.Ad, Deger: g.Sum(x => x.Deger)))
                     .GroupBy(g => g.Ad))
            gpuAd[g.Key] = Math.Min(100, g.Max(x => x.Deger));

        return cpuAd.Keys.Union(gpuAd.Keys)
            .Select(ad =>
            {
                var c = cpuAd.GetValueOrDefault(ad);
                return new SurecPayi(ad, Math.Max(c.Adet, 1), c.Cpu, gpuAd.GetValueOrDefault(ad),
                    cpuMW is { } mw && toplam > 0 ? mw / 1000 * c.Cpu / toplam : null);
            })
            .Where(p => p.Cpu >= Esik || p.Gpu >= Esik)
            .OrderByDescending(p => p.Cpu + p.Gpu)
            .ToList();
    }

    /// <summary>Uygulama özeti: her örnekteki paylar, örnek sayısına göre ortalanır (uygulamanın olmadığı saniye 0 sayılır).</summary>
    public static List<SurecOzeti> Ozet(IReadOnlyCollection<IReadOnlyList<SurecPayi>> ornekler)
    {
        var n = Math.Max(1, ornekler.Count);
        return ornekler.SelectMany(o => o).GroupBy(p => p.Ad)
            .Select(g => new SurecOzeti(g.Key, g.Sum(p => p.Cpu) / n, g.Sum(p => p.Gpu) / n,
                g.Any(p => p.Watt is not null) ? g.Sum(p => p.Watt ?? 0) / 3600 : null, g.Count()))
            .OrderByDescending(o => o.Cpu + o.Gpu)
            .ToList();
    }

    /// <summary>klasor/surecler altında <paramref name="gun"/> günden eski dosyaları siler (özet yok: hacmi büyük, ham CSV'nin yanında ikincil).</summary>
    public static void Temizle(string klasor, int gun, DateOnly bugun)
    {
        var k = Path.Combine(klasor, "surecler");
        if (!Directory.Exists(k)) return;
        foreach (var yol in Directory.EnumerateFiles(k, "*.csv"))
            if (DateOnly.TryParseExact(Path.GetFileNameWithoutExtension(yol), "yyyy-MM-dd", I, DateTimeStyles.None, out var t) && t < bugun.AddDays(-gun))
                try { File.Delete(yol); } catch (IOException) { }
    }

    List<(string, double)> Dizi(IntPtr sayac)
    {
        var l = new List<(string, double)>();
        if (sayac == IntPtr.Zero) return l;
        uint boyut = 0, adet = 0;
        if (PdhGetFormattedCounterArrayW(sayac, PdhFmtDouble, ref boyut, ref adet, IntPtr.Zero) != PdhMoreData) return l;
        var tampon = Marshal.AllocHGlobal((int)boyut);
        try
        {
            if (PdhGetFormattedCounterArrayW(sayac, PdhFmtDouble, ref boyut, ref adet, tampon) != 0) return l;
            var oge = Marshal.SizeOf<Oge>();
            for (var i = 0; i < adet; i++)
            {
                var o = Marshal.PtrToStructure<Oge>(tampon + i * oge);
                if (o.Durum == 0) l.Add((Marshal.PtrToStringUni(o.Ad) ?? "", o.Deger));
            }
        }
        finally { Marshal.FreeHGlobal(tampon); }
        return l;
    }

    public void Dispose()
    {
        _yazici?.Dispose();
        PdhCloseQuery(_sorgu);
    }

    const uint PdhFmtDouble = 0x200;
    const int PdhMoreData = unchecked((int)0x800007D2);

    [StructLayout(LayoutKind.Explicit)]
    struct Oge
    {
        [FieldOffset(0)] public IntPtr Ad;
        [FieldOffset(8)] public uint Durum;
        [FieldOffset(16)] public double Deger;
    }

    [DllImport("pdh.dll", CharSet = CharSet.Unicode)]
    static extern int PdhOpenQueryW(string? kaynak, IntPtr kullanici, out IntPtr sorgu);
    [DllImport("pdh.dll", CharSet = CharSet.Unicode)]
    static extern int PdhAddEnglishCounterW(IntPtr sorgu, string yol, IntPtr kullanici, out IntPtr sayac);
    [DllImport("pdh.dll")]
    static extern int PdhCollectQueryData(IntPtr sorgu);
    [DllImport("pdh.dll", CharSet = CharSet.Unicode)]
    static extern int PdhGetFormattedCounterArrayW(IntPtr sayac, uint bicim, ref uint boyut, ref uint adet, IntPtr tampon);
    [DllImport("pdh.dll")]
    static extern int PdhCloseQuery(IntPtr sorgu);
}
