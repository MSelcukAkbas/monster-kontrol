namespace MonsterPerformans.Cekirdek.Tests;

public class SurecKayitciTestleri
{
    [Fact]
    public void Alt_surecler_toplanir_esik_altı_duser_gpu_pidden_eslesir()
    {
        var cpu = new[] { ("chrome", 40.0), ("chrome#1", 24.0), ("Code", 4.0), ("Idle", 700.0), ("_Total", 800.0), ("dwm", 0.0) };
        var pid = new[] { ("chrome", 10.0), ("chrome#1", 11.0), ("Code", 20.0), ("dwm", 30.0) };
        var gpu = new[]
        {
            ("pid_30_luid_0x0_phys_0_eng_0_engtype_3D", 12.0),
            ("pid_11_luid_0x0_phys_0_eng_0_engtype_3D", 3.0),
            ("pid_10_luid_0x0_phys_0_eng_0_engtype_3D", 2.0),
            ("pid_10_luid_0x0_phys_0_eng_5_engtype_VideoDecode", 4.0),
        };
        var l = SurecKayitci.Grupla(cpu, pid, gpu, 8, 10_000);

        Assert.Equal(["chrome", "dwm"], l.Select(p => p.Ad)); // Code: 4/8 = %0,5 → düşer
        var c = l[0];
        Assert.Equal((2, 8.0, 5.0), (c.Adet, c.Cpu, c.Gpu)); // 64/8; 3D toplamı 5 > VideoDecode 4
        Assert.Equal(10 * 8 / 8.5, c.Watt!.Value, 3);         // toplam CPU payı 8 + 0,5
        Assert.Equal((12.0, 0.0), (l[1].Gpu, l[1].Watt));
    }

    [Fact]
    public void Ozet_ornek_sayisina_gore_ortalar_enerjiyi_toplar()
    {
        IReadOnlyList<SurecPayi>[] o =
        [
            [new("chrome", 3, 10, 20, 3.6), new("node", 1, 4, 0, 1.8)],
            [new("chrome", 3, 30, 0, 7.2)],
        ];
        var l = SurecKayitci.Ozet(o);
        Assert.Equal(new SurecOzeti("chrome", 20, 10, 0.003, 2), l[0]);
        Assert.Equal(new SurecOzeti("node", 2, 0, 0.0005, 1), l[1]); // olmadığı saniye 0 sayılır
    }
}
