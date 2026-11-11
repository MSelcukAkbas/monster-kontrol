using MonsterPerformans.Cekirdek.Fan;
using MonsterPerformans.Cekirdek.Guc;

namespace MonsterPerformans.Cekirdek.Tests;

public class AcilDurumTestleri
{
    static AcilKademe Calistir(AcilDurum a, int adet, int? cpu, int? gpu = null)
    {
        var k = a.Kademe;
        for (var i = 0; i < adet; i++) k = a.Adim(cpu, gpu);
        return k;
    }

    [Fact]
    public void Uc_ornek_ust_uste_gerekir()
    {
        var a = new AcilDurum();
        Assert.Equal(AcilKademe.Yok, Calistir(a, 2, 92));
        Assert.Equal(AcilKademe.Yok, a.Adim(91, null)); // sayaç sıfırlandı
        Assert.Equal(AcilKademe.Yok, Calistir(a, 2, 93));
        Assert.Equal(AcilKademe.Frekans, a.Adim(93, null));
    }

    [Fact]
    public void Gpu_87_de_tetikler() => Assert.Equal(AcilKademe.Frekans, Calistir(new AcilDurum(), 3, 60, 87));

    [Fact]
    public void Gpu_bos_okumalari_arada_tutulur()
    {
        var a = new AcilDurum();
        a.Adim(60, 88);
        a.Adim(60, null);
        Assert.Equal(AcilKademe.Frekans, a.Adim(60, 88));
    }

    [Fact]
    public void Uyuyan_gpu_5_sn_sonra_serin_sayilir()
    {
        var a = new AcilDurum();
        Calistir(a, 3, 95);
        a.Adim(80, 88);
        Assert.Equal(AcilKademe.Frekans, Calistir(a, 64, 80)); // ilk 5 sn GPU hâlâ 88 sayılır
        Assert.Equal(AcilKademe.Yok, a.Adim(80, null));
    }

    [Fact]
    public void Sicak_kaldikca_30_sn_de_bir_kademe_yukselir()
    {
        var a = new AcilDurum();
        Calistir(a, 3, 95);
        Assert.Equal(AcilKademe.Frekans, Calistir(a, 29, 95));
        Assert.Equal(AcilKademe.Fan, a.Adim(95, null));
        Assert.Equal(AcilKademe.Guc, Calistir(a, 30, 95));
        Assert.Equal(AcilKademe.Guc, Calistir(a, 100, 95)); // son kademe
    }

    [Fact]
    public void Ara_sicaklikta_kademe_yukselmez_ve_cikilmaz()
    {
        var a = new AcilDurum();
        Calistir(a, 3, 95);
        Assert.Equal(AcilKademe.Frekans, Calistir(a, 300, 88)); // 85–91: ne sıcak ne serin
    }

    [Fact]
    public void Altmis_sn_serinlikte_cikar_gecersiz_okuma_serin_sayilmaz()
    {
        var a = new AcilDurum();
        Calistir(a, 3, 95);
        Calistir(a, 59, 80);
        Assert.Equal(AcilKademe.Frekans, a.Adim(null, null)); // sensör hatası sayacı sıfırlar
        Assert.Equal(AcilKademe.Frekans, Calistir(a, 59, 80));
        Assert.Equal(AcilKademe.Frekans, a.Adim(80, 86)); // GPU hâlâ sıcak
        Assert.Equal(AcilKademe.Yok, Calistir(a, 60, 80, 70));
        Assert.Equal(AcilKademe.Yok, Calistir(a, 2, 95)); // yeniden 3 örnek gerekir
    }

    [Theory]
    [InlineData(90, 92)]
    [InlineData(94, 94)]
    [InlineData(99, 95)]
    public void Esik_92_95_arasinda_kalir(int verilen, int beklenen) => Assert.Equal(beklenen, new AcilDurum { Esik = verilen }.Esik);

    [Fact]
    public void Uyarla_birikimli_ve_frekansi_yukseltmez()
    {
        var p = HazirProfiller.Performans with { Fan = FanModu.Ozel };
        Assert.Same(p, AcilDurum.Uyarla(p, AcilKademe.Yok));
        Assert.Equal(p with { FrekansYuzde = 80 }, AcilDurum.Uyarla(p, AcilKademe.Frekans));
        Assert.Equal(p with { FrekansYuzde = 80, Fan = FanModu.Maksimum }, AcilDurum.Uyarla(p, AcilKademe.Fan));
        Assert.Equal(p with { FrekansYuzde = 60, Fan = FanModu.Maksimum, Guc = GucSeviyesi.Sessiz }, AcilDurum.Uyarla(p, AcilKademe.Guc));
        Assert.Equal(50, AcilDurum.Uyarla(p with { FrekansYuzde = 50 }, AcilKademe.Guc).FrekansYuzde);
    }

    /// <summary>11 dk gerçek yük kaydı (CPU en çok 91°C, TCC platosu): en düşük eşik 92 hiç tetiklenmemeli.</summary>
    [Fact]
    public void Gercek_yuk_kaydinda_tetiklenmez()
    {
        var dizin = new DirectoryInfo(AppContext.BaseDirectory);
        while (dizin is not null && !Directory.Exists(Path.Combine(dizin.FullName, "prototip"))) dizin = dizin.Parent;
        var yol = Path.Combine(dizin!.FullName, "prototip", "kayit", "2026-11-10_gercek_yuk.csv");
        var cpu = File.ReadLines(yol).Skip(1).Select(s => int.Parse(s.Split(',')[2])).ToList();
        Assert.True(cpu.Count > 600);
        Assert.Equal(91, cpu.Max());

        var a = new AcilDurum();
        Assert.All(cpu, c => Assert.Equal(AcilKademe.Yok, a.Adim(c, null)));
    }
}
