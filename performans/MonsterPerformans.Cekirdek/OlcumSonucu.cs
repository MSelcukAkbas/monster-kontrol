using System.Globalization;
using System.Text.Json;

namespace MonsterPerformans.Cekirdek;

/// <summary>"Ölçüm yap" özeti. Kıyas yalnız aynı profil, süre ve yük türündeki tam ölçümle yapılır (ısınma süresi sonucu değiştirir).</summary>
public sealed record OlcumSonucu(
    string Profil, DateTime Zaman, int Saniye, int Ornek,
    double CpuOrtC, int CpuEnYuksekC, int? GpuEnYuksekC, double? MhzOrt, double CpuDutyOrt, double? GpuWattOrt,
    bool GpuYuku, string? Kesildi)
{
    /// <summary>Geçersiz CPU okumaları atlanır. Hiç geçerli örnek yoksa null.</summary>
    public static OlcumSonucu? Ozetle(string profil, DateTime zaman, int saniye, IReadOnlyList<Olcum> o, bool gpuYuku, string? kesildi)
    {
        var g = o.Where(x => x.Dchu.Gecerli).ToList();
        if (g.Count == 0) return null;
        var gpuC = g.Select(x => x.Gpu?.C).OfType<int>().ToList();
        var mhz = g.Select(x => x.GercekMHz).OfType<double>().ToList();
        var watt = g.Select(x => x.Gpu?.Watt).OfType<double>().ToList();
        return new(profil, zaman, saniye, g.Count,
            g.Average(x => x.Dchu.CpuC), g.Max(x => x.Dchu.CpuC), gpuC.Count > 0 ? gpuC.Max() : null,
            mhz.Count > 0 ? mhz.Average() : null, g.Average(x => x.Dchu.CpuDuty), watt.Count > 0 ? watt.Average() : null,
            gpuYuku, kesildi);
    }

    static readonly CultureInfo Tr = CultureInfo.GetCultureInfo("tr-TR");

    public string Satir() => string.Format(Tr, "CPU ort. {0:0.0}°C, en çok {1}°C · {2} · fan %{3:0}{4}{5}",
        CpuOrtC, CpuEnYuksekC, MhzOrt is { } m ? string.Format(Tr, "{0:0.00} GHz", m / 1000) : "frekans yok", CpuDutyOrt,
        GpuEnYuksekC is { } g ? $" · GPU en çok {g}°C" + (GpuWattOrt is { } w ? string.Format(Tr, ", {0:0} W", w) : "") : "",
        (GpuYuku ? "" : " · yalnız CPU yükü") + (Kesildi is null ? "" : $" · {Kesildi}"));

    /// <summary>Tek satır: aynı profil ve süredeki bir önceki ölçüme göre fark. Yoksa null.</summary>
    public string? Kiyas(IEnumerable<OlcumSonucu> gecmis)
    {
        if (gecmis.Where(x => x.Profil == Profil && x.Saniye == Saniye && x.GpuYuku == GpuYuku && x.Kesildi is null && x.Zaman < Zaman)
                .MaxBy(x => x.Zaman) is not { } o) return null;
        var parca = new List<string> { string.Format(Tr, "CPU ort. {0:+0.0;−0.0;0}°C", CpuOrtC - o.CpuOrtC) };
        if (MhzOrt is { } m && o.MhzOrt is { } om) parca.Add(string.Format(Tr, "frekans {0:+0;−0;0} MHz", m - om));
        parca.Add(string.Format(Tr, "fan {0:+0;−0;0} puan", CpuDutyOrt - o.CpuDutyOrt));
        return $"{o.Zaman:dd.MM HH:mm} ölçümüne göre: " + string.Join(", ", parca);
    }

    public static string VarsayilanYol { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "MonsterPerformans", "olcumler.json");

    public static List<OlcumSonucu> Oku(string yol)
    {
        try { return JsonSerializer.Deserialize<List<OlcumSonucu>>(File.ReadAllText(yol)) ?? []; }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or JsonException or NotSupportedException) { return []; }
    }

    /// <summary>Son 200 ölçüm tutulur.</summary>
    public static void Ekle(string yol, OlcumSonucu s)
    {
        var l = Oku(yol);
        l.Add(s);
        Directory.CreateDirectory(Path.GetDirectoryName(yol)!);
        var gecici = yol + ".tmp";
        File.WriteAllText(gecici, JsonSerializer.Serialize(l.TakeLast(200), new JsonSerializerOptions { WriteIndented = true }));
        File.Move(gecici, yol, overwrite: true);
    }
}
