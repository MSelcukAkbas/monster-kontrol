using System.Buffers.Binary;
using MonsterKontrol.Dchu;

namespace MonsterPerformans.Cekirdek.Fan;

/// <summary>Bu modelde çalışan fan modları (0x79 alt 1). 2/5/8/9 bu modelde desteklenmiyor (docs/12 §5).</summary>
public enum FanModu { Otomatik = 0, Maksimum = 1, Ozel = 6 }

/// <summary>
/// Fan eğrisi ve modu. Eğri komut 14 ile EC belleğine gider (NVRAM'a değil), tampon 13'ten geri okunarak doğrulanır.
/// Özel moda yalnız doğrulanmış eğriyle geçilir; aksi halde güvenli duruma dönülür.
/// </summary>
public sealed class FanKontrol(IDchu dchu, TimeSpan? dogrulamaAraligi = null)
{
    internal const int EgriYazma = 14, EgriTamponu = 13;
    const int Deneme = 10;
    readonly TimeSpan _aralik = dogrulamaAraligi ?? TimeSpan.FromMilliseconds(100);

    public void ModAyarla(FanModu mod) => dchu.Yaz(Komutlar.Secenek, 0x0100_0000u | (uint)mod);

    /// <summary>Önce mod 0, sonra 0x69: yalnız 0x69 özel moddayken eğride bırakır (docs/01).</summary>
    public void GuvenliDurum()
    {
        ModAyarla(FanModu.Otomatik);
        dchu.Yaz(Komutlar.FanOtomatik, 0x0F);
    }

    /// <summary>EC'deki etkin CPU ve GPU eğrisi (2. ve 3. nokta).</summary>
    public (FanEgrisi Cpu, FanEgrisi Gpu) EcEgrisi()
    {
        var t = dchu.TamponOku(EgriTamponu);
        return (Coz(t, 16), Coz(t, 24));
    }

    /// <summary>Eğriyi yazar ve tampon 13'te görünene kadar bekler. Modu değiştirmez.</summary>
    /// <exception cref="ArgumentException">Eğri geçersiz; hiçbir şey yazılmadı.</exception>
    public bool EgriYaz(FanEgrisi cpu, FanEgrisi gpu)
    {
        var t = dchu.TamponOku(EgriTamponu);
        var hata = cpu.Hata(t[22]) is { } c ? "CPU: " + c : gpu.Hata(t[30]) is { } g ? "GPU: " + g : null;
        if (hata is not null)
            throw new ArgumentException(hata);

        dchu.TamponYaz(EgriYazma, Komut14(cpu, gpu));
        // Bekleme DCHU iş parçacığının dışında: örnekleyici durmasın
        for (var i = 0; i < Deneme; i++)
        {
            if (Dogrula(dchu.TamponOku(EgriTamponu), cpu, gpu))
                return true;
            Thread.Sleep(_aralik);
        }
        return false;
    }

    /// <summary>Eğriyi yazar, doğrulanırsa özel moda geçer. Doğrulanamazsa ya da hata olursa güvenli duruma döner.</summary>
    public bool OzelEgriUygula(FanEgrisi cpu, FanEgrisi gpu)
    {
        bool dogru;
        try { dogru = EgriYaz(cpu, gpu); }
        catch (ArgumentException) { throw; } // hiçbir şey yazılmadı
        catch { GuvenliDurum(); throw; }

        if (!dogru)
        {
            GuvenliDurum();
            return false;
        }
        ModAyarla(FanModu.Ozel);
        return true;
    }

    /// <summary>Komut 14 tamponu (fan sayfası baytları gösterir).</summary>
    public static byte[] Komut14(FanEgrisi cpu, FanEgrisi gpu)
    {
        var b = new byte[256];
        Yerlestir(b, 0, cpu);
        Yerlestir(b, 1, gpu);
        // 3. yuva (ikinci GPU fanı, bu modelde yok): CC'nin gönderdiği değerler (sayfa 4 dökümü, 11.11.2026)
        b[10] = 100; b[11] = 255; b[12] = 100; b[13] = 255; b[27] = 41;
        return b;
    }

    static void Yerlestir(byte[] b, int i, FanEgrisi e)
    {
        b[2 + 4 * i] = (byte)e.T2; b[3 + 4 * i] = FanEgrisi.Duty(e.D2);
        b[4 + 4 * i] = (byte)e.T3; b[5 + 4 * i] = FanEgrisi.Duty(e.D3);
        int[] egim =
        [
            Math.Max(0, FanEgrisi.Egim(FanEgrisi.T1, FanEgrisi.D1, e.T2, e.D2)), // d2 < %35: eksi eğim ushort'ta taşar; 0 canlıda çalıştı
            FanEgrisi.Egim(e.T2, e.D2, e.T3, e.D3),
            FanEgrisi.Egim(e.T3, e.D3, FanEgrisi.T4, FanEgrisi.D4),
        ];
        for (var j = 0; j < 3; j++)
            BinaryPrimitives.WriteUInt16BigEndian(b.AsSpan(14 + 6 * i + 2 * j), (ushort)egim[j]);
    }

    /// <summary>Eğimler tampon 13'te görünmez; yalnız 2. ve 3. nokta doğrulanabilir.</summary>
    internal static bool Dogrula(ReadOnlySpan<byte> t13, FanEgrisi cpu, FanEgrisi gpu)
    {
        var k = Komut14(cpu, gpu);
        return t13.Slice(18, 4).SequenceEqual(k.AsSpan(2, 4)) && t13.Slice(26, 4).SequenceEqual(k.AsSpan(6, 4));
    }

    // Tampon 13: fan başına 4 × (°C, duty); 2. ve 3. nokta ofset +2..+5
    static FanEgrisi Coz(byte[] t, int o) =>
        new(t[o + 2], FanEgrisi.Yuzde(t[o + 3]), t[o + 4], FanEgrisi.Yuzde(t[o + 5]));
}
