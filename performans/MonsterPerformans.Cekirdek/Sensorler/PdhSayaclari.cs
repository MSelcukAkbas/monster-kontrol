using System.Runtime.InteropServices;

namespace MonsterPerformans.Cekirdek.Sensorler;

/// <summary>
/// Windows performans sayaçları (sürücüsüz, yönetici gerekmez). Windows Türkçe olduğu için sayaçlar
/// İngilizce adla eklenir (PdhAddEnglishCounter). Okunamayan sayaç null döner.
/// </summary>
public sealed class PdhSayaclari : IDisposable
{
    /// <summary>İlk 6'nın sırası sabit (Olcum ve CSV sütunları buna dayanır); Sutun yalnız 6. ve sonrası için kullanılır.</summary>
    public static readonly (string Yol, string Sutun)[] Sayaclar =
    [
        (@"\Processor Information(_Total)\% Processor Time", "yuk"),
        (@"\Processor Information(_Total)\% Processor Performance", "perf"), // taban frekansa göre %, turboda > 100
        (@"\Processor Information(_Total)\Processor Frequency", "taban_mhz"), // taban (nominal) MHz — gerçek değil
        (@"\Processor Information(_Total)\Performance Limit Flags", "sinir_bayrak"),
        (@"\Processor Information(_Total)\% Performance Limit", "sinir_yuzde"),
        (@"\System\Processor Queue Length", "kuyruk"),
        (@"\Processor Information(_Total)\% Processor Utility", "utility"),
        (@"\Processor Information(_Total)\% Privileged Time", "ayricalikli"),
        (@"\Processor Information(_Total)\% Interrupt Time", "kesme_yuzde"),
        (@"\Processor Information(_Total)\% DPC Time", "dpc"),
        (@"\Processor Information(_Total)\Interrupts/sec", "kesme_sn"),
        (@"\Processor Information(_Total)\% C1 Time", "cdurum_c1"),
        (@"\Processor Information(_Total)\% C2 Time", "cdurum_c2"),
        (@"\Processor Information(_Total)\% C3 Time", "cdurum_c3"),
        (@"\System\Context Switches/sec", "baglam_gecis_sn"),
        (@"\System\System Calls/sec", "sistem_cagri_sn"),
        (@"\System\Processes", "surec"),
        (@"\System\Threads", "is_parcacigi"),
        (@"\Memory\Available MBytes", "bos_bellek_mb"),
        (@"\Memory\% Committed Bytes In Use", "taahhut_yuzde"),
        (@"\Memory\Pages/sec", "sayfa_sn"),
        (@"\PhysicalDisk(_Total)\% Disk Time", "disk_yuzde"),
        (@"\PhysicalDisk(_Total)\Disk Bytes/sec", "disk_bayt_sn"),
        // Intel RAPL (Windows Enerji Ölçer), miliwatt: paket = işlemcinin toplamı, pp0 = çekirdekler
        (@"\Energy Meter(rapl_package0_pkg)\Power", "cpu_mW"),
        (@"\Energy Meter(rapl_package0_pp0)\Power", "cekirdek_mW"),
        // Joker (*) tek değer döndürdüğü için her mantıksal çekirdek ayrı eklenir
        .. Enumerable.Range(0, Environment.ProcessorCount).SelectMany(i => new[]
        {
            ($@"\Processor Information(0,{i})\Actual Frequency", $"cek{i}_mhz"),
            ($@"\Processor Information(0,{i})\% Processor Time", $"cek{i}_yuk"),
        }),
    ];

    readonly IntPtr _sorgu;
    readonly IntPtr[] _sayaclar;

    public PdhSayaclari()
    {
        Kontrol(PdhOpenQueryW(null, IntPtr.Zero, out _sorgu), "PdhOpenQuery");
        _sayaclar = Sayaclar.Select(a => PdhAddEnglishCounterW(_sorgu, a.Yol, IntPtr.Zero, out var s) == 0 ? s : IntPtr.Zero).ToArray();
        PdhCollectQueryData(_sorgu); // oran sayaçları iki örnek ister; ilk Oku() anlamlı olsun
    }

    /// <summary>Sayaclar sırasıyla değerler.</summary>
    public double?[] Oku()
    {
        PdhCollectQueryData(_sorgu);
        return _sayaclar.Select(s =>
            s != IntPtr.Zero && PdhGetFormattedCounterValue(s, PdhFmtDouble, IntPtr.Zero, out var d) == 0 && d.Durum == 0
                ? d.Deger : (double?)null).ToArray();
    }

    public void Dispose() => PdhCloseQuery(_sorgu);

    static void Kontrol(int sonuc, string islem)
    {
        if (sonuc != 0) throw new InvalidOperationException($"{islem} başarısız: 0x{sonuc:X8}");
    }

    const uint PdhFmtDouble = 0x200;

    [StructLayout(LayoutKind.Explicit)]
    struct SayacDegeri
    {
        [FieldOffset(0)] public uint Durum;
        [FieldOffset(8)] public double Deger;
    }

    [DllImport("pdh.dll", CharSet = CharSet.Unicode)]
    static extern int PdhOpenQueryW(string? kaynak, IntPtr kullanici, out IntPtr sorgu);
    [DllImport("pdh.dll", CharSet = CharSet.Unicode)]
    static extern int PdhAddEnglishCounterW(IntPtr sorgu, string yol, IntPtr kullanici, out IntPtr sayac);
    [DllImport("pdh.dll")]
    static extern int PdhCollectQueryData(IntPtr sorgu);
    [DllImport("pdh.dll")]
    static extern int PdhGetFormattedCounterValue(IntPtr sayac, uint bicim, IntPtr tur, out SayacDegeri deger);
    [DllImport("pdh.dll")]
    static extern int PdhCloseQuery(IntPtr sorgu);
}
