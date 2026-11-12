using System.Runtime.InteropServices;

namespace MonsterPerformans.Cekirdek.Sensorler;

/// <summary>NVIDIA kartının aygıt güç durumu (D0–D3), Windows aygıt yöneticisinden: okumak kartı uyandırmaz.</summary>
public static class GucDurumu
{
    static readonly Guid GoruntuSinifi = new("4d36e968-e325-11ce-bfc1-08002be10318");
    static readonly DevPropKey GucVerisi = new() { Fmtid = new("a45c254e-df1c-4efd-8020-67d146a850e0"), Pid = 32 }; // DEVPKEY_Device_PowerData
    static readonly Lazy<uint?> Dugum = new(DugumBul);

    /// <summary>Kart D0 değilse true. Aygıt bulunamaz ya da okunamazsa false (eski davranış: NVML'e sorulur).</summary>
    public static bool Uyuyor()
    {
        if (Dugum.Value is not { } d) return false;
        var b = new byte[64];
        var boy = (uint)b.Length;
        if (CM_Get_DevNode_PropertyW(d, GucVerisi, out _, b, ref boy, 0) != 0 || boy < 8) return false;
        return BitConverter.ToInt32(b, 4) > 1; // CM_POWER_DATA.PD_MostRecentPowerState: 1 = D0
    }

    static uint? DugumBul()
    {
        const uint SinifSuzgeci = 0x200 | 0x100; // CM_GETIDLIST_FILTER_CLASS | PRESENT
        var sinif = GoruntuSinifi.ToString("B");
        if (CM_Get_Device_ID_List_SizeW(out var boy, sinif, SinifSuzgeci) != 0) return null;
        var liste = new char[boy];
        if (CM_Get_Device_ID_ListW(sinif, liste, boy, SinifSuzgeci) != 0) return null;
        var id = new string(liste).Split('\0').FirstOrDefault(s => s.Contains("VEN_10DE", StringComparison.OrdinalIgnoreCase));
        return id is not null && CM_Locate_DevNodeW(out var d, id, 0) == 0 ? d : null;
    }

    struct DevPropKey { public Guid Fmtid; public uint Pid; }

    [DllImport("cfgmgr32.dll", CharSet = CharSet.Unicode)]
    static extern int CM_Get_Device_ID_List_SizeW(out uint boy, string suzgec, uint bayrak);
    [DllImport("cfgmgr32.dll", CharSet = CharSet.Unicode)]
    static extern int CM_Get_Device_ID_ListW(string suzgec, char[] tampon, uint boy, uint bayrak);
    [DllImport("cfgmgr32.dll", CharSet = CharSet.Unicode)]
    static extern int CM_Locate_DevNodeW(out uint dugum, string id, uint bayrak);
    [DllImport("cfgmgr32.dll")]
    static extern int CM_Get_DevNode_PropertyW(uint dugum, in DevPropKey anahtar, out uint tur, byte[] tampon, ref uint boy, uint bayrak);
}
