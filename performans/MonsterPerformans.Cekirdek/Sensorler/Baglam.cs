using System.Diagnostics;
using System.Runtime.InteropServices;
using MonsterPerformans.Cekirdek.Guc;

namespace MonsterPerformans.Cekirdek.Sensorler;

/// <summary>
/// O saniyede ne yapılıyordu ve Windows hangi ayardaydı (docs/10 §3b bağlam). Ön plan için yalnız
/// program dosyasının adı tutulur, pencere başlığı tutulmaz.
/// </summary>
public readonly record struct Baglam(
    string? OnPlan, int? Bildirim, bool? Priz, int? Pil, int? BostaSn,
    string? Plan, uint? FrekansSiniri, uint? Turbo, uint? Epp);

/// <summary>Yönetici gerekmez; her alan ayrı okunur, okunamayan null kalır.</summary>
public sealed class BaglamOkuyucu(IGucPlani plan)
{
    static readonly Guid Epp = new("36687f9e-e3a5-4dbf-b1dc-15eb381c6863"); // PERFEPP

    int _pid;
    string? _ad;

    public Baglam Oku()
    {
        bool? priz = null;
        int? pil = null;
        if (GetSystemPowerStatus(out var g))
        {
            priz = g.AcLine switch { 0 => false, 1 => true, _ => null };
            pil = g.PilYuzde == 255 ? null : g.PilYuzde;
        }

        string? planAdi = null;
        uint? siniri = null, turbo = null, epp = null;
        try
        {
            var p = plan.Etkin;
            planAdi = WindowsGucPlani.Ad(p);
            uint? Ayar(Guid a) { var (ac, dc) = plan.IslemciAyariOku(p, a); return priz == false ? dc : ac; }
            siniri = Ayar(WindowsGucPlani.FrekansSiniri);
            turbo = Ayar(WindowsGucPlani.TurboModu);
            epp = Ayar(Epp);
        }
        catch (InvalidOperationException) { } // okunabilenler kalır (EPP plan da yoksa yalnız o boş)

        var son = new SonGiris { Boy = (uint)Marshal.SizeOf<SonGiris>() };
        int? bosta = GetLastInputInfo(ref son) ? (int)((uint)Environment.TickCount - son.Zaman) / 1000 : null;

        return new(OnPlan(), SHQueryUserNotificationState(out var b) == 0 ? b : null, priz, pil, bosta,
            planAdi, siniri, turbo, epp);
    }

    string? OnPlan()
    {
        GetWindowThreadProcessId(GetForegroundWindow(), out var pid);
        if (pid == 0) return null;
        if (pid != _pid)
        {
            _pid = (int)pid;
            try { using var p = Process.GetProcessById(_pid); _ad = p.ProcessName; }
            catch (Exception e) when (e is ArgumentException or InvalidOperationException) { _ad = null; }
        }
        return _ad;
    }

    [StructLayout(LayoutKind.Sequential)]
    struct GucDurumu { public byte AcLine, Bayrak, PilYuzde, Sistem; public int Omur, TamOmur; }

    [StructLayout(LayoutKind.Sequential)]
    struct SonGiris { public uint Boy, Zaman; }

    [DllImport("kernel32.dll")] static extern bool GetSystemPowerStatus(out GucDurumu d);
    [DllImport("user32.dll")] static extern bool GetLastInputInfo(ref SonGiris s);
    [DllImport("user32.dll")] static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr pencere, out uint pid);
    // 1 ekran koruyucu/kilit yok, 2 meşgul (tam ekran), 3 D3D tam ekran, 4 sunum, 5 kabul ediyor, 7 uygulama (Windows 8+)
    [DllImport("shell32.dll")] static extern int SHQueryUserNotificationState(out int durum);
}
