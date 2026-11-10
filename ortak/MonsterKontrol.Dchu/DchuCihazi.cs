using System.Collections.Concurrent;
using System.Reflection;
using System.Runtime.InteropServices;

namespace MonsterKontrol.Dchu;

/// <summary>
/// Gerçek DCHU erişimi. Tüm DLL çağrıları tek bir adanmış iş parçacığında sırayla yapılır
/// (DLL'in eşzamanlılık güvencesi bilinmiyor). Yönetici izni gerekmez.
/// </summary>
public sealed class DchuCihazi : IDchu, IDisposable
{
    // DLL 256 baytlık veriden sonra 257. bayta 0 yazıyor; 256'lık tampon yığını bozup 0xC0000005 ile
    // süreci düşürüyordu (docs/01). Bu yüzden tampon geniş, dışarıya yalnız 256 bayt verilir.
    internal const int TamponBoyu = 512;
    public const int VeriBoyu = 256;

    static int _cozucuKuruldu;

    readonly BlockingCollection<Action> _kuyruk = new();
    readonly Thread _isParcacigi;

    public string DllYolu { get; }

    public DchuCihazi(string? dllYolu = null)
    {
        DllYolu = dllYolu ?? DllBulucu.Bul();
        CozucuyuKur(DllYolu);
        _isParcacigi = new Thread(Dongu) { IsBackground = true, Name = "DCHU" };
        _isParcacigi.Start();
        // DLL'i şimdi yükle: yüklenemiyorsa hata kurucuda çıksın, ilk komutta değil
        Calistir(() => NativeLibrary.Load(DllYolu));
    }

    public void Yaz(int komut, uint arguman)
    {
        var veri = Komutlar.Bayt(arguman);
        Calistir(() => Yerel.SetDCHU_Data(komut, veri, veri.Length));
    }

    public byte[] TamponOku(int komut) => Calistir(() =>
    {
        var tampon = new byte[TamponBoyu];
        Yerel.GetDCHU_Data_Buffer(komut, ref tampon[0]);
        return tampon[..VeriBoyu];
    });

    public int TamsayiOku(int komut) => Calistir(() =>
    {
        Yerel.GetDCHU_Data_Integer(komut, out var deger);
        return deger;
    });

    T Calistir<T>(Func<T> islem)
    {
        if (Thread.CurrentThread == _isParcacigi)
            return islem();
        var sonuc = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
        _kuyruk.Add(() =>
        {
            try { sonuc.SetResult(islem()); }
            catch (Exception e) { sonuc.SetException(e); }
        });
        return sonuc.Task.GetAwaiter().GetResult();
    }

    void Dongu()
    {
        foreach (var islem in _kuyruk.GetConsumingEnumerable())
            islem();
    }

    public void Dispose()
    {
        _kuyruk.CompleteAdding();
        _isParcacigi.Join(TimeSpan.FromSeconds(2));
        _kuyruk.Dispose();
    }

    /// <summary>"InsydeDCHU" adını bulunan tam yola yönlendirir (DriverStore yolu çalışma anında bulunur).</summary>
    static void CozucuyuKur(string yol)
    {
        if (Interlocked.Exchange(ref _cozucuKuruldu, 1) == 1)
            return;
        NativeLibrary.SetDllImportResolver(typeof(Yerel).Assembly, (ad, _, _) =>
            ad == Yerel.Kutuphane ? NativeLibrary.Load(yol) : IntPtr.Zero);
    }

    /// <summary>İmzalar Control Center'ın kendi P/Invoke'larından birebir alındı (prototip/imzalar.ps1).</summary>
    static class Yerel
    {
        public const string Kutuphane = "InsydeDCHU";

        [DllImport(Kutuphane)]
        public static extern int SetDCHU_Data(int komut, byte[] veri, int uzunluk);

        [DllImport(Kutuphane)]
        public static extern int GetDCHU_Data_Buffer(int komut, ref byte tampon);

        [DllImport(Kutuphane)]
        public static extern int GetDCHU_Data_Integer(int komut, out int deger);
    }
}
