using System.Runtime.InteropServices;

namespace MonsterPerformans.Cekirdek.Sensorler;

/// <summary>
/// Windows performans sayaçları (sürücüsüz, yönetici gerekmez). Windows Türkçe olduğu için sayaçlar
/// İngilizce adla eklenir (PdhAddEnglishCounter). Okunamayan sayaç null döner.
/// </summary>
public sealed class PdhSayaclari : IDisposable
{
    public static readonly string[] Adlar =
    [
        @"\Processor Information(_Total)\% Processor Time",        // yük
        @"\Processor Information(_Total)\% Processor Performance", // taban frekansa göre %, turboda > 100
        @"\Processor Information(_Total)\Processor Frequency",     // taban (nominal) MHz — gerçek değil
        @"\Processor Information(_Total)\Performance Limit Flags",
        @"\Processor Information(_Total)\% Performance Limit",
        @"\System\Processor Queue Length",
    ];

    readonly IntPtr _sorgu;
    readonly IntPtr[] _sayaclar;

    public PdhSayaclari()
    {
        Kontrol(PdhOpenQueryW(null, IntPtr.Zero, out _sorgu), "PdhOpenQuery");
        _sayaclar = Adlar.Select(ad => PdhAddEnglishCounterW(_sorgu, ad, IntPtr.Zero, out var s) == 0 ? s : IntPtr.Zero).ToArray();
        PdhCollectQueryData(_sorgu); // oran sayaçları iki örnek ister; ilk Oku() anlamlı olsun
    }

    /// <summary>Adlar sırasıyla değerler.</summary>
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
