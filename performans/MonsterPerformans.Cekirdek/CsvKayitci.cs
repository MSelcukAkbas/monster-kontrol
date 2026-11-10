using System.Globalization;
using System.Text;

namespace MonsterPerformans.Cekirdek;

/// <summary>
/// Ham kayıt: günde bir CSV (klasör/yyyy-MM-dd.csv), ekleyerek yazar, her satırda diske boşaltır
/// (çökmede en çok bir satır kaybolur). Ondalık ayırıcı nokta (kültürden bağımsız).
/// </summary>
public sealed class CsvKayitci(string klasor) : IDisposable
{
    public const string Baslik =
        "zaman,cpu_C,kart_C,cpu_duty,cpu_rpm,gpu_duty,gpu_rpm,profil_b30,yuk,perf,taban_mhz,gercek_mhz,sinir_bayrak,sinir_yuzde,kuyruk," +
        "gpu_C,gpu_kullanim,gpu_W,gpu_mhz,ham";

    StreamWriter? _yazici;
    DateOnly _gun;

    public static string Satir(Olcum o)
    {
        var i = CultureInfo.InvariantCulture;
        string S(double? d) => d?.ToString("0.#", i) ?? "";
        var d = o.Dchu;
        var g = o.Gpu;
        return string.Join(',',
            o.Zaman.ToString("HH:mm:ss", i), d.CpuC, d.KartC, d.CpuDuty, d.CpuRpm, d.GpuDuty, d.GpuRpm, d.ProfilBayti,
            S(o.Pdh[0]), S(o.Pdh[1]), S(o.Pdh[2]), S(o.GercekMHz), S(o.Pdh[3]), S(o.Pdh[4]), S(o.Pdh[5]),
            S(g?.C), S(g?.Kullanim), S(g?.Watt), S(g?.MHz),
            Convert.ToHexString(o.HamTampon));
    }

    public void Yaz(Olcum o)
    {
        var gun = DateOnly.FromDateTime(o.Zaman);
        if (_yazici is null || gun != _gun)
        {
            _yazici?.Dispose();
            Directory.CreateDirectory(klasor);
            var yol = Path.Combine(klasor, $"{gun:yyyy-MM-dd}.csv");
            var yeni = !File.Exists(yol);
            _yazici = new StreamWriter(yol, append: true, new UTF8Encoding(false)) { AutoFlush = true };
            if (yeni) _yazici.WriteLine(Baslik);
            _gun = gun;
        }
        _yazici.WriteLine(Satir(o));
    }

    public void Dispose() => _yazici?.Dispose();
}
