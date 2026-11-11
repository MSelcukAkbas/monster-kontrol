using System.Runtime.InteropServices;

namespace MonsterPerformans.Cekirdek.Sensorler;

/// <summary>Kisit: NVML saat kısma nedenleri bit maskesi (güç, ısı, boşta…). VramMB: kullanılan video belleği.</summary>
public readonly record struct GpuDurumu(int? C, int? Kullanim, double? Watt, int? MHz,
    int? BellekKullanim = null, int? BellekMHz = null, int? PDurumu = null, ulong? Kisit = null, int? VramMB = null);

/// <summary>
/// NVIDIA GPU sıcaklığı/gücü (nvml.dll sürücüyle gelir; DCHU bayt 17 GPU çekirdeği değil, docs/01).
/// GPU uykudayken (Optimus) çağrılar hata döner → o alan null. nvml.dll yoksa Ac() null döner.
/// </summary>
public sealed class Nvml : IDisposable
{
    readonly IntPtr _cihaz;

    Nvml(IntPtr cihaz) => _cihaz = cihaz;

    public static Nvml? Ac()
    {
        try
        {
            if (nvmlInit_v2() != 0) return null;
            if (nvmlDeviceGetHandleByIndex_v2(0, out var cihaz) == 0) return new Nvml(cihaz);
            nvmlShutdown();
        }
        catch (DllNotFoundException) { }
        catch (EntryPointNotFoundException) { }
        return null;
    }

    public GpuDurumu Oku()
    {
        Kullanim? k = nvmlDeviceGetUtilizationRates(_cihaz, out var kk) == 0 ? kk : null;
        return new(
            nvmlDeviceGetTemperature(_cihaz, 0, out var c) == 0 ? (int)c : null,
            (int?)k?.Gpu,
            nvmlDeviceGetPowerUsage(_cihaz, out var mw) == 0 ? mw / 1000.0 : null,
            nvmlDeviceGetClockInfo(_cihaz, 0, out var mhz) == 0 ? (int)mhz : null,
            (int?)k?.Bellek,
            nvmlDeviceGetClockInfo(_cihaz, 2, out var bmhz) == 0 ? (int)bmhz : null, // 2 = NVML_CLOCK_MEM
            nvmlDeviceGetPerformanceState(_cihaz, out var ps) == 0 ? ps : null,
            nvmlDeviceGetCurrentClocksThrottleReasons(_cihaz, out var kisit) == 0 ? kisit : null,
            nvmlDeviceGetMemoryInfo(_cihaz, out var bel) == 0 ? (int)(bel.Kullanilan / (1024 * 1024)) : null);
    }

    public void Dispose() => nvmlShutdown();

    struct Kullanim { public uint Gpu, Bellek; }

    struct Bellek { public ulong Toplam, Bos, Kullanilan; }

    const string Dll = "nvml.dll";
    [DllImport(Dll)] static extern int nvmlInit_v2();
    [DllImport(Dll)] static extern int nvmlShutdown();
    [DllImport(Dll)] static extern int nvmlDeviceGetHandleByIndex_v2(uint sira, out IntPtr cihaz);
    [DllImport(Dll)] static extern int nvmlDeviceGetTemperature(IntPtr cihaz, int sensor, out uint c);
    [DllImport(Dll)] static extern int nvmlDeviceGetUtilizationRates(IntPtr cihaz, out Kullanim k);
    [DllImport(Dll)] static extern int nvmlDeviceGetPowerUsage(IntPtr cihaz, out uint miliwatt);
    [DllImport(Dll)] static extern int nvmlDeviceGetClockInfo(IntPtr cihaz, int tur, out uint mhz);
    [DllImport(Dll)] static extern int nvmlDeviceGetPerformanceState(IntPtr cihaz, out int pDurumu);
    [DllImport(Dll)] static extern int nvmlDeviceGetCurrentClocksThrottleReasons(IntPtr cihaz, out ulong nedenler);
    [DllImport(Dll)] static extern int nvmlDeviceGetMemoryInfo(IntPtr cihaz, out Bellek bellek);
}
