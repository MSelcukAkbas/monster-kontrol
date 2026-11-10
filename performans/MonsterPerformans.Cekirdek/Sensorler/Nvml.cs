using System.Runtime.InteropServices;

namespace MonsterPerformans.Cekirdek.Sensorler;

public readonly record struct GpuDurumu(int? C, int? Kullanim, double? Watt, int? MHz);

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

    public GpuDurumu Oku() => new(
        nvmlDeviceGetTemperature(_cihaz, 0, out var c) == 0 ? (int)c : null,
        nvmlDeviceGetUtilizationRates(_cihaz, out var k) == 0 ? (int)k.Gpu : null,
        nvmlDeviceGetPowerUsage(_cihaz, out var mw) == 0 ? mw / 1000.0 : null,
        nvmlDeviceGetClockInfo(_cihaz, 0, out var mhz) == 0 ? (int)mhz : null);

    public void Dispose() => nvmlShutdown();

    struct Kullanim { public uint Gpu, Bellek; }

    const string Dll = "nvml.dll";
    [DllImport(Dll)] static extern int nvmlInit_v2();
    [DllImport(Dll)] static extern int nvmlShutdown();
    [DllImport(Dll)] static extern int nvmlDeviceGetHandleByIndex_v2(uint sira, out IntPtr cihaz);
    [DllImport(Dll)] static extern int nvmlDeviceGetTemperature(IntPtr cihaz, int sensor, out uint c);
    [DllImport(Dll)] static extern int nvmlDeviceGetUtilizationRates(IntPtr cihaz, out Kullanim k);
    [DllImport(Dll)] static extern int nvmlDeviceGetPowerUsage(IntPtr cihaz, out uint miliwatt);
    [DllImport(Dll)] static extern int nvmlDeviceGetClockInfo(IntPtr cihaz, int tur, out uint mhz);
}
