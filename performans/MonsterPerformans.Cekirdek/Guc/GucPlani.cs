using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;

namespace MonsterPerformans.Cekirdek.Guc;

/// <summary>Windows güç planları (powrprof). Testlerde sahte uygulama kullanılır.</summary>
public interface IGucPlani
{
    /// <summary>Temel planın uygulamaya ait kopyası; yoksa oluşturur. Aynı temel için hep aynı GUID.</summary>
    Guid KopyaHazirla(Guid temel);

    /// <summary>İşlemci alt grubundaki bir ayarı hem prizde (AC) hem pilde (DC) yazar.</summary>
    void IslemciAyariYaz(Guid plan, Guid ayar, uint deger);

    (uint Ac, uint Dc) IslemciAyariOku(Guid plan, Guid ayar);

    Guid Etkin { get; }

    /// <summary>Değişiklikler ancak plan yeniden etkinleştirilince işler (docs/02); etkin olsa da çağır.</summary>
    void Etkinlestir(Guid plan);
}

public sealed class WindowsGucPlani : IGucPlani
{
    public static readonly Guid Dengeli = new("381b4222-f694-41f0-9685-ff5bb260df2e");
    public static readonly Guid AltIslemci = new("54533251-82be-4824-96c1-47b60b740d00");
    public static readonly Guid TurboModu = new("be337238-0d82-4146-a960-4f3749d470c7");     // PERFBOOSTMODE
    public static readonly Guid FrekansSiniri = new("75b0ae3f-bce0-45a7-8c89-c9611c25e100"); // PROCFREQMAX, MHz, 0 = sınırsız

    const uint ZatenVar = 183;
    public const string KopyaOnEki = "MK · ";

    /// <summary>Sistemdeki planlar; uygulamanın kendi kopyaları hariç (kopyanın kopyası olmasın).</summary>
    public static IReadOnlyList<(Guid Guid, string Ad)> Planlar()
    {
        var l = new List<(Guid, string)>();
        for (uint i = 0; ; i++)
        {
            var b = new byte[16];
            var n = (uint)b.Length;
            if (PowerEnumerate(IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, 16 /* ACCESS_SCHEME */, i, b, ref n) != 0)
                break; // 259 = ERROR_NO_MORE_ITEMS
            var g = new Guid(b);
            var ad = Ad(g);
            if (!ad.StartsWith(KopyaOnEki, StringComparison.Ordinal)) l.Add((g, ad));
        }
        return l;
    }

    /// <summary>Kopyanın GUID'i temelden türetilir: her açılışta yeni plan oluşmaz.</summary>
    public static Guid KopyaGuid(Guid temel) =>
        new(MD5.HashData([.. temel.ToByteArray(), .. "MonsterKontrol"u8]));

    public Guid KopyaHazirla(Guid temel)
    {
        var kopya = KopyaGuid(temel);
        var p = Marshal.AllocHGlobal(16);
        try
        {
            Marshal.StructureToPtr(kopya, p, false);
            var r = PowerDuplicateScheme(IntPtr.Zero, ref temel, ref p);
            if (r == ZatenVar)
                return kopya;
            Denetle(r, "PowerDuplicateScheme");
        }
        finally { Marshal.FreeHGlobal(p); }

        var ad = Encoding.Unicode.GetBytes(KopyaOnEki + Ad(temel) + "\0");
        Denetle(PowerWriteFriendlyName(IntPtr.Zero, ref kopya, IntPtr.Zero, IntPtr.Zero, ad, (uint)ad.Length), "PowerWriteFriendlyName");
        return kopya;
    }

    public void IslemciAyariYaz(Guid plan, Guid ayar, uint deger)
    {
        var alt = AltIslemci;
        Denetle(PowerWriteACValueIndex(IntPtr.Zero, ref plan, ref alt, ref ayar, deger), "PowerWriteACValueIndex");
        Denetle(PowerWriteDCValueIndex(IntPtr.Zero, ref plan, ref alt, ref ayar, deger), "PowerWriteDCValueIndex");
    }

    public (uint Ac, uint Dc) IslemciAyariOku(Guid plan, Guid ayar)
    {
        var alt = AltIslemci;
        Denetle(PowerReadACValueIndex(IntPtr.Zero, ref plan, ref alt, ref ayar, out var ac), "PowerReadACValueIndex");
        Denetle(PowerReadDCValueIndex(IntPtr.Zero, ref plan, ref alt, ref ayar, out var dc), "PowerReadDCValueIndex");
        return (ac, dc);
    }

    public Guid Etkin
    {
        get
        {
            Denetle(PowerGetActiveScheme(IntPtr.Zero, out var p), "PowerGetActiveScheme");
            try { return Marshal.PtrToStructure<Guid>(p); }
            finally { LocalFree(p); }
        }
    }

    public void Etkinlestir(Guid plan) => Denetle(PowerSetActiveScheme(IntPtr.Zero, ref plan), "PowerSetActiveScheme");

    /// <summary>Etkin plan silinemez: önce başka planı etkinleştir.</summary>
    public void Sil(Guid plan) => Denetle(PowerDeleteScheme(IntPtr.Zero, ref plan), "PowerDeleteScheme");

    public static string Ad(Guid plan)
    {
        var b = new byte[512];
        var n = (uint)b.Length;
        Denetle(PowerReadFriendlyName(IntPtr.Zero, ref plan, IntPtr.Zero, IntPtr.Zero, b, ref n), "PowerReadFriendlyName");
        return Encoding.Unicode.GetString(b, 0, (int)n).TrimEnd('\0');
    }

    // DCHU'nun aksine powrprof gerçek Win32 hata kodu döndürür
    static void Denetle(uint r, string islev)
    {
        if (r != 0)
            throw new InvalidOperationException($"{islev} hata {r}");
    }

    const string P = "powrprof.dll";
    [DllImport(P)] static extern uint PowerDuplicateScheme(IntPtr kok, ref Guid kaynak, ref IntPtr hedef);
    [DllImport(P)] static extern uint PowerWriteFriendlyName(IntPtr kok, ref Guid plan, IntPtr alt, IntPtr ayar, byte[] ad, uint boy);
    [DllImport(P)] static extern uint PowerReadFriendlyName(IntPtr kok, ref Guid plan, IntPtr alt, IntPtr ayar, byte[] ad, ref uint boy);
    [DllImport(P)] static extern uint PowerWriteACValueIndex(IntPtr kok, ref Guid plan, ref Guid alt, ref Guid ayar, uint deger);
    [DllImport(P)] static extern uint PowerWriteDCValueIndex(IntPtr kok, ref Guid plan, ref Guid alt, ref Guid ayar, uint deger);
    [DllImport(P)] static extern uint PowerReadACValueIndex(IntPtr kok, ref Guid plan, ref Guid alt, ref Guid ayar, out uint deger);
    [DllImport(P)] static extern uint PowerReadDCValueIndex(IntPtr kok, ref Guid plan, ref Guid alt, ref Guid ayar, out uint deger);
    [DllImport(P)] static extern uint PowerGetActiveScheme(IntPtr kok, out IntPtr plan);
    [DllImport(P)] static extern uint PowerSetActiveScheme(IntPtr kok, ref Guid plan);
    [DllImport(P)] static extern uint PowerDeleteScheme(IntPtr kok, ref Guid plan);
    [DllImport(P)] static extern uint PowerEnumerate(IntPtr kok, IntPtr plan, IntPtr alt, uint erisim, uint indeks, byte[] tampon, ref uint boy);
    [DllImport("kernel32.dll")] static extern IntPtr LocalFree(IntPtr p);
}
